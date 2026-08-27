using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Networking.ManagedActions;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 交易（Tradeable）：把带"交易"关键词的卡（如火热促销）拖到弃牌堆上方松手后，
/// 点击弃牌堆触发交易——洗入你的弃牌堆，然后从抽牌堆抽一张牌。
/// 交互流程：拖牌松手（未打出，卡取消回手牌）时记录最后拖拽的卡；
/// 3 秒内点击弃牌堆 → 请求交易动作（替代打开弃牌堆查看）。
/// 联机同步：交易动作通过 RitsuLib 托管网络动作（<see cref="RitsuLibManagedNetActions"/>）
/// 在两端同步执行——原版没有牌堆同步消息，牌堆变更必须发生在 networked 动作内
/// 两端同步执行；旧实现是本地 fire-and-forget 改牌堆，联机中会直接导致
/// StateDivergence 断联（且旧实现拦截 TryPlayCard，拖到弃牌堆（Play Zone 外）松手
/// 根本不会调用 TryPlayCard，交易从未触发）。
/// </summary>
public static class JainaTradeTracker
{
    /// <summary>最后拖拽（取消）的卡。</summary>
    public static CardModel? LastDraggedCard { get; private set; }

    /// <summary>最后拖拽时间（毫秒时间戳）。</summary>
    private static long _lastDragTimeMs;

    /// <summary>点击弃牌堆触发交易的窗口（拖拽取消后多久内点击有效）。</summary>
    private const long TradeWindowMs = 3000;

    /// <summary>3 秒内拖拽过带"交易"关键词的卡。</summary>
    public static bool HasRecentTradeableDrag()
    {
        return LastDraggedCard != null &&
               LastDraggedCard.Keywords.Contains(JainaKeywords.Tradeable) &&
               Environment.TickCount64 - _lastDragTimeMs < TradeWindowMs;
    }

    internal static void RecordDrag(CardModel card)
    {
        LastDraggedCard = card;
        _lastDragTimeMs = Environment.TickCount64;
    }

    internal static void Clear()
    {
        LastDraggedCard = null;
        _lastDragTimeMs = 0;
    }
}

/// <summary>
/// 拖牌取消（松手未打出/右键取消）时记录最后拖拽的卡（供点击弃牌堆交易用）。
/// 正常打出（TryPlayCard 成功）不走取消，不会记录。
/// </summary>
[HarmonyPatch(typeof(NCardPlay), "CancelPlayCard")]
public static class JainaTradeDragPatch
{
    public static void Postfix(NCardPlay __instance)
    {
        try
        {
            var card = __instance.Holder?.CardModel;
            if (card == null)
            {
                return;
            }
            JainaTradeTracker.RecordDrag(card);
        }
        catch
        {
            // 记录失败不影响原流程
        }
    }
}

/// <summary>
/// 点击弃牌堆：3 秒内拖拽过带"交易"关键词的卡 → 请求交易动作（替代打开弃牌堆）。
/// </summary>
[HarmonyPatch(typeof(NCombatCardPile), "OnRelease")]
public static class JainaTradeDiscardClickPatch
{
    public static bool Prefix(NCombatCardPile __instance)
    {
        try
        {
            if (__instance is not NDiscardPileButton)
            {
                return true;
            }
            if (!JainaTradeTracker.HasRecentTradeableDrag())
            {
                return true;
            }
            var card = JainaTradeTracker.LastDraggedCard!;
            JainaTradeTracker.Clear();
            RequestTrade(card);
            return false; // 交易替代打开弃牌堆
        }
        catch
        {
            return true;
        }
    }

    private static void RequestTrade(CardModel card)
    {
        var player = card.Owner;
        if (player == null || player.Creature?.CombatState == null)
        {
            return;
        }
        var payload = new TradePayload { CombatCardIndex = NetCombatCard.FromModel(card).CombatCardIndex };
        JainaTradeAction.Request(payload, player);
    }
}

/// <summary>
/// 交易动作载荷：联机两端按战斗卡索引（NetCombatCard.CombatCardIndex）定位同一张卡。
/// </summary>
public sealed record TradePayload
{
    public uint CombatCardIndex { get; set; }
}

/// <summary>
/// 交易网络动作：发起端入队后两端同步执行——洗入弃牌堆 + 从抽牌堆抽一张牌（顶牌）。
/// </summary>
public static class JainaTradeAction
{
    private static readonly RitsuLibManagedNetActionDescriptor<TradePayload> Descriptor = new(
        "jaina",
        "tradeable-trade-v1",
        p => JsonSerializer.SerializeToUtf8Bytes(p),
        b => JsonSerializer.Deserialize<TradePayload>(b) ?? new TradePayload(),
        ExecuteAsync,
        GameActionType.CombatPlayPhaseOnly);

    /// <summary>请求交易动作（由操作玩家本机发起；两端同步执行）。</summary>
    public static bool Request(TradePayload payload, Player player)
    {
        return RitsuLibManagedNetActions.Request(RunManager.Instance, Descriptor, payload, player.NetId);
    }

    private static async Task ExecuteAsync(RitsuLibManagedNetActionContext<TradePayload> ctx)
    {
        try
        {
            var player = ctx.Player;
            if (player == null || player.Creature?.CombatState == null)
            {
                return;
            }
            var card = NetCombatCard.ForTesting(ctx.Message.CombatCardIndex).ToCardModelOrNull();
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[JainaTrade] resolve id={ctx.Message.CombatCardIndex} card={(card == null ? "null" : $"{card.Id.Entry}({card.GetType().Name})")} pile={card?.Pile?.Type} owner={(card?.Owner?.NetId)} player={player.NetId}");
            if (card == null || card.Pile?.Type != PileType.Hand || card.Owner != player)
            {
                return; // 卡已不在手牌（排队期间被打出等）：跳过
            }
            // 防御：只有带"交易"关键词的卡可被交易（若两端战斗卡索引/ID 数据库不一致,
            // 索引解析到非交易卡时跳过——防止把错误的卡洗入弃牌堆导致状态分歧）
            if (!card.Keywords.Contains(JainaKeywords.Tradeable))
            {
                MegaCrit.Sts2.Core.Logging.Log.Warn(
                    $"[JainaTrade] resolve id={ctx.Message.CombatCardIndex} -> non-tradeable card {card.Id.Entry}, skip");
                return;
            }
            // 洗入弃牌堆
            await CardPileCmd.Add(card, PileType.Discard);
            // 从抽牌堆抽一张牌（顶牌入手；无牌可抽不抽）
            var drawPile = player.PlayerCombatState?.DrawPile;
            if (drawPile != null && drawPile.Cards.Count > 0)
            {
                var top = drawPile.Cards.FirstOrDefault();
                if (top != null)
                {
                    await CardPileCmd.Add(top, PileType.Hand);
                }
            }
        }
        catch (Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] Tradeable trade action failed: {ex}");
        }
    }
}
