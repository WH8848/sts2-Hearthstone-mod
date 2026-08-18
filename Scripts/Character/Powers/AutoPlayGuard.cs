using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 随机释放（自动打出 AutoPlay）上下文守卫：
/// - <see cref="IsInAutoPlay"/>：调用栈检测当前是否处于 AutoPlay 流程
///   （C# async 状态机帧 &lt;AutoPlay&gt;d__xx.MoveNext 或 CardCmd.AutoPlay 同步帧）——
///   联机两端同步执行同一 AutoPlay 流程，栈结构一致（确定性）。
/// - 随机选择辅助：随机释放过程中<b>玩家不可操作</b>——随机释放的卡遇到玩家选择机制
///   （发现三选一/从手牌或牌堆选卡等）时自动随机选择，不弹界面不暂停。
/// </summary>
public static class AutoPlayGuard
{
    /// <summary>
    /// 最近一次 AutoPlay 的卡实例（实例标记）：
    /// 调用栈检测在 RitsuLib/RegentFX 的 async 包装下可能失效（OnPlay 延续脱离 AutoPlay 帧），
    /// 用"发起选择的卡 == 最近 AutoPlay 的卡"实例引用对比兜底。
    /// 由 CardCmd.AutoPlay 的 Prefix 更新（手打不经过 AutoPlay → 不更新）。
    /// </summary>
    public static CardModel? CurrentAutoPlayCard;

    /// <summary>
    /// 当前是否处于 AutoPlay 上下文（调用栈检测 + 实例标记双保险）。
    /// <paramref name="source"/> = 发起选择的卡（如 CardSelectCmd.FromHand 的 source 参数）。
    /// </summary>
    public static bool IsAutoPlayContext(CardModel? source)
    {
        if (IsInAutoPlay())
        {
            return true;
        }
        return source != null && CurrentAutoPlayCard != null && ReferenceEquals(source, CurrentAutoPlayCard);
    }

    /// <summary>
    /// 当前调用栈是否处于自动打出（AutoPlay）流程中。
    /// C# async 方法在栈上显示为状态机帧（方法名 MoveNext、声明类型形如
    /// &lt;AutoPlay&gt;d__xx），同时保留原方法名 AutoPlay 的同步帧——两种都检测。
    /// 在玩家选择入口（CardSelectCmd 各方法）与发现完成（AddGeneratedCardToCombat）时调用，
    /// 频率低，性能可接受。
    /// </summary>
    public static bool IsInAutoPlay()
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
    /// 从候选中随机选一张（filter 过滤后；无候选返回 null）。
    /// 用 CombatTargets RNG——两端同步执行同一流程，结果一致（确定性）。
    /// </summary>
    public static CardModel? PickRandom(Player player, IEnumerable<CardModel>? candidates,
        Func<CardModel, bool>? filter = null)
    {
        if (player == null || candidates == null)
        {
            return null;
        }
        var list = candidates.Where(c => c != null && (filter == null || filter(c))).ToList();
        if (list.Count == 0)
        {
            return null;
        }
        return player.RunState.Rng.CombatTargets.NextItem(list);
    }

    /// <summary>
    /// 从候选中随机选 count 张（filter 过滤后；候选不足全给；不重复）。
    /// </summary>
    public static List<CardModel> PickRandomN(Player player, IEnumerable<CardModel>? candidates,
        int count, Func<CardModel, bool>? filter = null)
    {
        var picked = new List<CardModel>();
        if (player == null || candidates == null || count <= 0)
        {
            return picked;
        }
        var list = candidates.Where(c => c != null && (filter == null || filter(c))).ToList();
        while (picked.Count < count && list.Count > 0)
        {
            var card = player.RunState.Rng.CombatTargets.NextItem(list);
            if (card == null)
            {
                break;
            }
            list.Remove(card);
            picked.Add(card);
        }
        return picked;
    }
}
