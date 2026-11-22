using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 按战斗和玩家串行执行需要等待的状态变更，避免多个发现/伤害流程交错。
/// </summary>
public static class JainaSerialExecutor
{
    private sealed class QueueState
    {
        public readonly Queue<Func<Task>> Items = new();
        public bool IsRunning;
    }

    private static readonly ConditionalWeakTable<ICombatState,
        ConditionalWeakTable<Player, QueueState>> Queues = new();

    public static void Enqueue(ICombatState combatState, Player player, Func<Task> operation)
    {
        var players = Queues.GetValue(combatState, _ => new ConditionalWeakTable<Player, QueueState>());
        var queue = players.GetValue(player, _ => new QueueState());
        bool start;
        lock (queue)
        {
            queue.Items.Enqueue(operation);
            start = !queue.IsRunning;
            queue.IsRunning = true;
        }
        if (start)
        {
            _ = TaskHelper.RunSafely(DrainAsync(queue));
        }
    }

    private static async Task DrainAsync(QueueState queue)
    {
        while (true)
        {
            Func<Task>? operation;
            lock (queue)
            {
                if (queue.Items.Count == 0)
                {
                    queue.IsRunning = false;
                    return;
                }
                operation = queue.Items.Dequeue();
            }
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] serialized operation failed: {ex}");
            }
        }
    }
}
