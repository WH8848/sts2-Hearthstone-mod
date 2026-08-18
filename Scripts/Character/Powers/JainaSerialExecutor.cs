using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 联机安全的主线程串行执行器：
/// 把 fire-and-forget 的异步触发任务按到达顺序排队，逐个串行执行——
/// 前一个任务完全结束（所有 await 完成）后才启动下一个。
///
/// 为什么必须串行：多个并发异步任务（如回合开始同时抽到多张惊奇卡牌，
/// 各自触发随机施放）中，`Cmd.Wait` 等 await 的恢复时序在两端机器上
/// 可能不同（帧率/线程调度差异）→ 动作执行顺序两端相反 →
/// Pile Play 顺序不同 + OnPlayWrapper 的 PlayerChoiceContext 栈交错
/// （"Tried to pop model X but Y was on the top of the stack"）→
/// checksum 分歧（StateDivergence）断联。
/// 串行化后执行顺序 = 到达顺序（抽牌/打出顺序，两端确定一致）。
///
/// 线程模型：必须在 Godot 主线程调用 <see cref="Enqueue"/>（钩子/Postfix 内），
/// 排空循环在该线程启动并随 await 恢复回到该线程执行。
/// </summary>
public static class JainaSerialExecutor
{
    private sealed record Pending(
        PlayerChoiceContext ChoiceContext,
        CardModel Card,
        Func<PlayerChoiceContext, CardModel, Task> Action);

    private static readonly object _lock = new();
    private static readonly Queue<Pending> _queue = new();
    private static bool _running;

    /// <summary>
    /// 按调用顺序排队执行异步动作（若已在排空则追加到队尾）。
    /// </summary>
    public static void Enqueue(PlayerChoiceContext choiceContext, CardModel card,
        Func<PlayerChoiceContext, CardModel, Task> action)
    {
        lock (_lock)
        {
            _queue.Enqueue(new Pending(choiceContext, card, action));
            if (_running)
            {
                return;
            }
            _running = true;
        }
        _ = DrainAsync();
    }

    private static async Task DrainAsync()
    {
        try
        {
            while (true)
            {
                Pending item;
                lock (_lock)
                {
                    if (_queue.Count == 0)
                    {
                        _running = false;
                        return;
                    }
                    item = _queue.Dequeue();
                }
                try
                {
                    await item.Action(item.ChoiceContext, item.Card);
                }
                catch (Exception ex)
                {
                    MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] serial executor task failed: {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _running = false;
            }
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] serial executor drain failed: {ex}");
        }
    }
}
