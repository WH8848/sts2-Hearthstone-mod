using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 手牌移出前记录当时能响应最右侧出牌的露娜。
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AddDuringManualCardPlay))]
internal static class JainaHandOrderTracker
{
    private static readonly ConditionalWeakTable<CardModel, List<LunaMinion>> RightmostPlays = new();

    private static void Prefix(CardModel card)
    {
        RightmostPlays.Remove(card);
        var player = card.Owner;
        var hand = player.PlayerCombatState?.Hand.Cards;
        if (hand == null || hand.Count == 0 || hand[^1] != card ||
            player.Creature.CombatState is not { } combat)
        {
            return;
        }

        var lunas = combat.Creatures
            .Where(creature => creature.IsAlive && creature.PetOwner == player)
            .Select(creature => creature.Monster)
            .OfType<LunaMinion>()
            .ToList();
        if (lunas.Count > 0)
        {
            RightmostPlays.Add(card, lunas);
        }
    }

    internal static bool ConsumeRightmostPlay(CardPlay play, LunaMinion luna)
    {
        if (play.IsAutoPlay || play.PlayIndex != play.PlayCount - 1 ||
            !RightmostPlays.TryGetValue(play.Card, out var lunas) || !lunas.Remove(luna))
        {
            return false;
        }

        if (lunas.Count == 0)
        {
            RightmostPlays.Remove(play.Card);
        }
        return true;
    }
}
