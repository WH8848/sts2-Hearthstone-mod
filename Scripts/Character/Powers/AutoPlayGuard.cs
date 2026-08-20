using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MegaCrit.Sts2.Core.Combat;
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
    /// <b>AsyncLocal 实现</b>：值沿<b>异步流程</b>流动而非全局共享——联机双人并行操作时
    /// （一方玩家手打、另一方随机释放链进行中），手打流程的清空/设置不会污染并行的
    /// AutoPlay 流程，反之亦然。否则全局静态字段会被并行的玩家手打清空，
    /// 导致随机释放链中后续选择入口 AutoPick 判定失败（MISS）→ 弹玩家选择界面暂停
    /// → 两端动作执行顺序分歧 → StateDivergence 断联（匣中古神 + 队友同时打牌必现）。
    /// </summary>
    private static readonly AsyncLocal<CardModel?> CurrentAutoPlayCardAsyncLocal = new();

    /// <summary>
    /// 当前流程最近的 AutoPlay 卡实例（AsyncLocal，流程隔离）
    /// </summary>
    public static CardModel? CurrentAutoPlayCard
    {
        get => CurrentAutoPlayCardAsyncLocal.Value;
        set => CurrentAutoPlayCardAsyncLocal.Value = value;
    }

    /// <summary>
    /// 当前 AutoPlay 是否由<b>吉安娜 mod 的随机释放机制</b>发起
    /// （匣中古神/惊奇卡牌/魔法智慧之球/大法师的符文/冰血哨塔/戏法图腾/重放等吉安娜卡牌发起的释放——
    /// 释放的卡可以是原版/其它 mod 的卡）。
    /// AutoPickSelectionPatch 只对吉安娜发起的随机释放自动选；
    /// 其它 mod 的随机释放（原版倾泻/其它 mod 机制）触发的选择正常弹界面。
    /// 设置：OnPlayWrapper(isAutoPlay=false) 手打吉安娜卡时置 true；非打出触发的吉安娜施法
    /// （球/哨塔/图腾回合结束施法）在施放入口显式置 true；CombatEnded 清空。
    /// <b>AsyncLocal 实现</b>：同上——并行玩家操作互不污染（全局静态字段在双人
    /// 并行操作时会互相覆盖，导致一端 AutoPick 失效/误判）。
    /// </summary>
    private static readonly AsyncLocal<bool> CurrentAutoPlayIsJainaOriginAsyncLocal = new();

    /// <summary>
    /// 当前流程是否为吉安娜机制发起的随机释放（AsyncLocal，流程隔离）
    /// </summary>
    public static bool CurrentAutoPlayIsJainaOrigin
    {
        get => CurrentAutoPlayIsJainaOriginAsyncLocal.Value;
        set => CurrentAutoPlayIsJainaOriginAsyncLocal.Value = value;
    }

    /// <summary>
    /// 该卡是否属于<b>吉安娜 mod</b>（卡类型定义在 jaina 程序集）。
    /// </summary>
    public static bool IsJainaCard(CardModel? card)
    {
        return card != null && card.GetType().Assembly == typeof(AutoPlayGuard).Assembly;
    }

    /// <summary>
    /// 当前是否处于 AutoPlay 上下文（调用栈检测 + 实例标记双保险）。
    /// <paramref name="source"/> = 发起选择的卡（如 CardSelectCmd.FromHand 的 source 参数）。
    /// <b>只对吉安娜 mod 的随机释放机制发起的 AutoPlay 自动选</b>
    /// （<see cref="CurrentAutoPlayIsJainaOrigin"/>——手打吉安娜卡/球哨塔图腾施法置位）；
    /// 其它 mod 的随机释放、战斗外选择一律正常弹界面。
    /// </summary>
    public static bool IsAutoPlayContext(CardModel? source)
    {
        // 战斗外：不存在随机释放上下文，任何选择都正常弹界面
        if (!CombatManager.Instance.IsInProgress)
        {
            return false;
        }
        // 仅吉安娜 mod 发起的随机释放自动选（其它 mod/原版的 AutoPlay 选择正常弹界面）
        if (!CurrentAutoPlayIsJainaOrigin)
        {
            return false;
        }
        if (IsInAutoPlay())
        {
            return true;
        }
        // 栈检测失效兜底：AutoPlay 进行中（实例标记非空）
        if (CurrentAutoPlayCard != null)
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
