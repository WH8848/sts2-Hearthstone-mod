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
///
/// 支持连续多次发现（如广阔智慧连发现两张）：候选按开始顺序累积为多个集合，
/// 完成记录按序入队，消费方按自己的 _lastSeq 逐个消费，不再互相覆盖。
/// </summary>
public static class DiscoverTracker
{
    /// <summary>进行中的发现候选集合列表（玩家 → 按开始顺序的候选集合）</summary>
    private static readonly ConditionalWeakTable<Player, List<HashSet<CardModel>>> Active = new();

    /// <summary>已完成待消费的发现记录队列（玩家 → 按完成顺序）</summary>
    private static readonly ConditionalWeakTable<Player, List<PendingDiscover>> Pending = new();

    private static long _seq;

    /// <summary>
    /// 一次发现完成：选中的卡 + 其余选项（自动使用用）
    /// </summary>
    public sealed class PendingDiscover
    {
        public required CardModel Selected { get; init; }

        public required IReadOnlyList<CardModel> Others { get; init; }

        public long Seq { get; init; }

        /// <summary>
        /// 该发现是否由<b>随机释放</b>（自动打出 AutoPlay）触发——
        /// 源生之石（自动使用其余选项）不响应随机释放触发的发现。
        /// 联机两端同步执行同一 AutoPlay 流程 → 调用栈结构一致（确定性）。
        /// </summary>
        public bool IsAuto { get; init; }
    }

    /// <summary>
    /// 发现界面开始（FromChooseACardScreen 前缀）：追加记录本次候选集合
    /// （不清空旧集合，支持连续多次发现同时进行）
    /// </summary>
    public static void BeginDiscover(Player player, IReadOnlyList<CardModel> cards)
    {
        if (player == null)
        {
            return;
        }
        var list = Active.GetValue(player, _ => []);
        var set = new HashSet<CardModel>();
        if (cards != null)
        {
            set.UnionWith(cards);
        }
        list.Add(set);
    }

    /// <summary>
    /// 卡置入手牌时调用（AddGeneratedCardToCombat 前缀）：
    /// 若该卡属于某个进行中的发现候选（按最近的候选集合优先匹配），
    /// 记录一次"发现完成"并入队，返回 true。
    /// </summary>
    public static bool OnCardAddedToHand(Player player, CardModel card)
    {
        if (card == null || player == null)
        {
            return false;
        }
        if (!Active.TryGetValue(player, out var list))
        {
            return false;
        }
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var set = list[i];
            if (!set.Remove(card))
            {
                continue;
            }
            // 匹配到：从候选集合中移除（该集合中其余卡即"其余选项"）
            var queue = Pending.GetValue(player, _ => []);
            queue.Add(new PendingDiscover
            {
                Selected = card,
                Others = set.ToList(),
                Seq = Interlocked.Increment(ref _seq),
                // 随机释放（AutoPlay）流程中触发的发现：源生之石不响应
                IsAuto = IsInAutoPlayCallStack()
            });
            return true;
        }
        return false;
    }

    /// <summary>
    /// 当前调用栈是否处于自动打出（AutoPlay）流程中。
    /// C# async 方法在栈上显示为状态机帧（方法名 MoveNext、声明类型形如
    /// &lt;AutoPlay&gt;d__xx），同时保留原方法名 AutoPlay 的同步帧——
    /// 两种都检测。发现完成（AddGeneratedCardToCombat 前缀）时调用，
    /// 频率低（每次发现一次），性能可接受。
    /// </summary>
    private static bool IsInAutoPlayCallStack()
    {
        var stack = new System.Diagnostics.StackTrace(2, false);
        for (int i = 0; i < stack.FrameCount; i++)
        {
            var method = stack.GetFrame(i)?.GetMethod();
            if (method == null)
            {
                continue;
            }
            if (method.Name == "AutoPlay")
            {
                return true;
            }
            var declaring = method.DeclaringType;
            if (declaring != null && declaring.Name.Contains("AutoPlay", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 取玩家队列中第一条 Seq 大于 afterSeq 的发现记录（无则 null）。
    /// 消费方维护自己的 _lastSeq，可逐个消费全部未处理记录。
    /// </summary>
    public static PendingDiscover? TryGetPending(Player player, long afterSeq)
    {
        if (!Pending.TryGetValue(player, out var queue))
        {
            return null;
        }
        return queue.FirstOrDefault(p => p.Seq > afterSeq);
    }

    /// <summary>
    /// 该玩家当前已入队的最大发现 Seq（无记录返回 0）。
    /// 任务卡打出时用作计数起点——跳过打出之前已完成的发现，
    /// 保证"打出此能力后，才开始计数任务进度"。
    /// </summary>
    public static long GetLatestSeq(Player player)
    {
        if (!Pending.TryGetValue(player, out var queue) || queue.Count == 0)
        {
            return 0;
        }
        return queue.Max(p => p.Seq);
    }
}
