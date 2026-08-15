using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 发现追踪：识别"从三张随机卡牌中选择一张置入手牌"（发现）事件。
/// 游戏所有发现（含原版发现/药水/遗物与 mod 内发现）都走
/// CardSelectCmd.FromChooseACardScreen 选择 + CardPileCmd.AddGeneratedCardToCombat 入手；
/// 通过这两个入口的 Prefix 精确判定"发现完成"（选中卡 ∈ 进行中候选）。
/// 发现的消费方（禁忌序列计数/源生之石）在卡打出完成（AfterCardPlayed/
/// AfterPotionUsed，有 PlayerChoiceContext）时读取待处理记录，用递增 Seq 保证
/// 多个消费方各自只处理一次。
/// </summary>
public static class DiscoverTracker
{
    /// <summary>进行中的发现候选（玩家 → 候选卡集合）</summary>
    private static readonly ConditionalWeakTable<Player, ActiveRecord> Active = new();

    /// <summary>最新一次"发现完成"待处理记录（玩家 → 记录）</summary>
    private static readonly ConditionalWeakTable<Player, PendingDiscover> Pending = new();

    private static long _seq;

    private sealed class ActiveRecord
    {
        public readonly HashSet<CardModel> Candidates = [];
    }

    /// <summary>
    /// 一次发现完成：选中的卡 + 其余选项（自动使用用）
    /// </summary>
    public sealed class PendingDiscover
    {
        public required CardModel Selected { get; init; }

        public required IReadOnlyList<CardModel> Others { get; init; }

        public long Seq { get; init; }
    }

    /// <summary>
    /// 发现界面开始（FromChooseACardScreen 前缀）：记录候选
    /// </summary>
    public static void BeginDiscover(Player player, IReadOnlyList<CardModel> cards)
    {
        var rec = Active.GetOrCreateValue(player);
        rec.Candidates.Clear();
        if (cards != null)
        {
            rec.Candidates.UnionWith(cards);
        }
    }

    /// <summary>
    /// 卡置入手牌时调用（AddGeneratedCardToCombat 前缀）：
    /// 若该卡属于进行中的发现候选，记录一次"发现完成"，返回 true。
    /// </summary>
    public static bool OnCardAddedToHand(Player player, CardModel card)
    {
        if (card == null || player == null)
        {
            return false;
        }
        if (!Active.TryGetValue(player, out var rec) || !rec.Candidates.Remove(card))
        {
            return false;
        }
        Pending.Remove(player);
        Pending.Add(player, new PendingDiscover
        {
            Selected = card,
            Others = rec.Candidates.ToList(),
            Seq = Interlocked.Increment(ref _seq)
        });
        return true;
    }

    /// <summary>
    /// 取玩家最新一次发现完成记录（无则 null）
    /// </summary>
    public static PendingDiscover? TryGetPending(Player player)
    {
        return Pending.TryGetValue(player, out var pending) ? pending : null;
    }
}
