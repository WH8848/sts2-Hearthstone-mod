using System;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
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
/// 交易（Tradeable）：把带“交易”关键词的卡拖到弃牌堆上方松手，立即将其弃掉并抽一张牌。
/// 普通点击弃牌堆仍然打开弃牌堆界面。
/// </summary>
public static class JainaTradeTracker
{
    private static NMouseCardPlay? _pendingPlay;
    private static CardModel? _pendingCard;
    private static bool _pendingDiscardDrop;

    /// <summary>记录一次鼠标左键释放，并判断释放位置是否命中弃牌堆。</summary>
    internal static void RecordLeftRelease(NMouseCardPlay play)
    {
        Clear(play);

        var card = play.Holder?.CardModel;
        var discardPile = NCombatRoom.Instance?.Ui.DiscardPile;
        if (card == null || discardPile == null ||
            !card.Keywords.Contains(JainaKeywords.Tradeable))
        {
            return;
        }

        var mousePosition = play.GetViewport().GetMousePosition();
        if (!discardPile.GetGlobalRect().HasPoint(mousePosition))
        {
            return;
        }

        _pendingPlay = play;
        _pendingCard = card;
        _pendingDiscardDrop = true;
    }

    /// <summary>交易拖拽命中弃牌堆时阻止弃牌堆界面打开。</summary>
    internal static bool ShouldConsumeDiscardRelease(NCombatCardPile pile)
    {
        return pile is NDiscardPileButton && _pendingDiscardDrop;
    }

    /// <summary>卡牌取消拖拽时执行交易，其他取消操作只清理暂存状态。</summary>
    internal static void HandleCancel(NMouseCardPlay play)
    {
        if (!ReferenceEquals(_pendingPlay, play))
        {
            return;
        }

        var card = _pendingCard;
        var shouldTrade = _pendingDiscardDrop && card != null;
        Clear(play);
        if (shouldTrade)
        {
            JainaTradeAction.Request(card!);
        }
    }

    /// <summary>卡牌进入正常出牌流程时清理拖拽状态。</summary>
    internal static void Clear(NCardPlay play)
    {
        if (!ReferenceEquals(_pendingPlay, play))
        {
            return;
        }

        _pendingPlay = null;
        _pendingCard = null;
        _pendingDiscardDrop = false;
    }
}

/// <summary>记录左键释放位置，用于识别拖牌释放到弃牌堆的操作。</summary>
[HarmonyPatch(typeof(NMouseCardPlay), "_Input")]
public static class JainaTradeMouseReleasePatch
{
    public static void Postfix(NMouseCardPlay __instance, InputEvent inputEvent)
    {
        try
        {
            if (inputEvent is InputEventMouseButton
                {
                    ButtonIndex: MouseButton.Left
                } mouseButton && mouseButton.IsReleased())
            {
                JainaTradeTracker.RecordLeftRelease(__instance);
            }
        }
        catch
        {
            // 识别失败不影响原版拖牌流程。
        }
    }
}

/// <summary>拖牌取消时执行命中弃牌堆的交易。</summary>
[HarmonyPatch(typeof(NMouseCardPlay), "OnCancelPlayCard")]
public static class JainaTradeCancelPatch
{
    public static void Prefix(NMouseCardPlay __instance)
    {
        try
        {
            JainaTradeTracker.HandleCancel(__instance);
        }
        catch
        {
            // 交易识别失败不影响原版取消流程。
        }
    }
}

/// <summary>正常出牌时清理未完成的拖拽识别状态。</summary>
[HarmonyPatch(typeof(NCardPlay), "TryPlayCard")]
public static class JainaTradePlayPatch
{
    public static void Prefix(NCardPlay __instance)
    {
        JainaTradeTracker.Clear(__instance);
    }
}

/// <summary>交易拖牌释放到弃牌堆时，阻止普通弃牌堆界面打开。</summary>
[HarmonyPatch(typeof(NCombatCardPile), "OnRelease")]
public static class JainaTradeDiscardReleasePatch
{
    public static bool Prefix(NCombatCardPile __instance)
    {
        try
        {
            return !JainaTradeTracker.ShouldConsumeDiscardRelease(__instance);
        }
        catch
        {
            return true;
        }
    }
}

/// <summary>交易动作载荷：联机两端按战斗卡索引定位同一张卡。</summary>
public sealed record TradePayload
{
    public uint CombatCardIndex { get; set; }
}

/// <summary>交易网络动作：弃掉交易卡后抽一张牌，两端通过托管动作同步执行。</summary>
public static class JainaTradeAction
{
    private static readonly RitsuLibManagedNetActionDescriptor<TradePayload> Descriptor = new(
        "jaina",
        "tradeable-trade-v1",
        p => JsonSerializer.SerializeToUtf8Bytes(p),
        b => JsonSerializer.Deserialize<TradePayload>(b) ?? new TradePayload(),
        ExecuteAsync,
        GameActionType.CombatPlayPhaseOnly);

    /// <summary>确保托管动作描述符在所有进程尽早注册。</summary>
    public static void EnsureRegistered()
    {
        _ = Descriptor;
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[JainaTrade] managed-action descriptor registered: {Descriptor.ModuleId}/{Descriptor.ActionKey} ({GameActionType.CombatPlayPhaseOnly})");
    }

    /// <summary>请求交易动作。</summary>
    public static bool Request(CardModel card)
    {
        var player = card.Owner;
        if (player == null || player.Creature?.CombatState == null)
        {
            return false;
        }

        var payload = new TradePayload
        {
            CombatCardIndex = NetCombatCard.FromModel(card).CombatCardIndex
        };
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
            if (card == null || card.Pile?.Type != PileType.Hand || card.Owner != player ||
                !card.Keywords.Contains(JainaKeywords.Tradeable))
            {
                return;
            }

            // 使用原版弃牌并抽牌命令，保留弃牌钩子、抽牌钩子、动画和满手牌规则。
            await CardCmd.DiscardAndDraw(
                ctx.PlayerChoiceContext,
                new[] { card },
                1);
        }
        catch (Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] Tradeable trade action failed: {ex}");
        }
    }
}
