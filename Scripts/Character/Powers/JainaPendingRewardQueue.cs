using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 按战斗和玩家保存等待入手的卡牌效果。手牌腾出空位后，按顺序结算。
/// </summary>
public static class JainaPendingRewardQueue
{
    private sealed class PendingRewards
    {
        public readonly Queue<Func<PlayerChoiceContext, Task>> Grants = new();
        public bool IsFlushing;
    }

    private static readonly ConditionalWeakTable<ICombatState, ConditionalWeakTable<Player, PendingRewards>> Queues = new();

    public static Task GrantOrQueue(PlayerChoiceContext choiceContext, Player player, CardModel card) =>
        GrantOrQueue(choiceContext, player, _ => CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player));

    internal static Task GrantOrQueue(PlayerChoiceContext choiceContext, Player player, Func<PlayerChoiceContext, Task> grant)
    {
        if (player.Creature.CombatState is not { } combatState || CombatManager.Instance.IsOverOrEnding)
        {
            return Task.CompletedTask;
        }

        var pending = Queues.GetOrCreateValue(combatState).GetOrCreateValue(player);
        lock (pending)
        {
            pending.Grants.Enqueue(grant);
        }
        return TryFlush(combatState, player, choiceContext);
    }

    private static async Task TryFlush(ICombatState combatState, Player player, PlayerChoiceContext? choiceContext)
    {
        if (!Queues.TryGetValue(combatState, out var players) ||
            !players.TryGetValue(player, out var pending))
        {
            return;
        }

        lock (pending)
        {
            if (pending.IsFlushing)
            {
                return;
            }
            pending.IsFlushing = true;
        }
        try
        {
            while (ReferenceEquals(player.Creature.CombatState, combatState) &&
                   player.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding && JainaHandHelper.CanAddToHand(player))
            {
                Func<PlayerChoiceContext, Task>? grant;
                lock (pending)
                {
                    grant = pending.Grants.Count > 0 ? pending.Grants.Dequeue() : null;
                }
                if (grant == null)
                {
                    break;
                }
                try
                {
                    await grant(choiceContext ??= new BlockingPlayerChoiceContext());
                }
                catch (System.Exception ex)
                {
                    MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] pending reward grant failed: {ex}");
                }
            }
        }
        finally
        {
            lock (pending)
            {
                pending.IsFlushing = false;
            }
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardChangedPiles))]
    private static class HandSpacePatch
    {
        private static void Postfix(ICombatState? combatState, CardModel card, PileType oldPile, ref Task __result)
        {
            if (oldPile == PileType.Hand && combatState != null && card.Owner is { } player)
            {
                __result = FlushAfterPileChange(__result, combatState, player);
            }
        }

        private static async Task FlushAfterPileChange(Task pileChange, ICombatState combatState, Player player)
        {
            await pileChange;
            await TryFlush(combatState, player, null);
        }
    }
}
