using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜发现（Discover）工具：从吉安娜的攻击/技能牌池中随机抽取若干张供玩家选择。
/// 发现池：火球术、寒冰箭、火焰冲击、奥术智慧、冰冻药水、寒冰护盾。
/// </summary>
public static class JainaDiscoverHelper
{
    /// <summary>
    /// 吉安娜攻击/技能牌池（可被发现）
    /// </summary>
    private static readonly Type[] AttackSkillPool =
    [
        typeof(Fireball),
        typeof(Frostbolt),
        typeof(Fireblast),
        typeof(ArcaneIntellect),
        typeof(FreezingPotion),
        typeof(IceBarrier)
    ];

    /// <summary>
    /// 从发现池中随机选若干张（不重复），可过滤费用上限
    /// </summary>
    public static List<CardModel> RollCandidates(Player player, int count = 3, int? maxCost = null)
    {
        // 用 CreateCard 生成带 Owner 的实例（MutableClone 的卡无 Owner，AddGeneratedCardToCombat 会 NRE）
        var combatState = player.Creature.CombatState;
        var pool = AttackSkillPool
            .Select(t => combatState.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(t)), player))
            .ToList();
        if (maxCost is int max && max >= 0)
        {
            pool = pool.Where(c => c.EnergyCost.Canonical <= max).ToList();
        }
        var picked = new List<CardModel>();
        while (picked.Count < count && pool.Count > 0)
        {
            var card = player.RunState.Rng.CombatTargets.NextItem(pool);
            if (card == null)
            {
                break;
            }
            picked.Add(card);
            pool.Remove(card);
        }
        return picked;
    }

    /// <summary>
    /// 三选一发现（可跳过），选中的牌加入手牌
    /// </summary>
    public static async Task<CardModel?> DiscoverAndAddToHand(PlayerChoiceContext choiceContext, Player player, int count = 3, int? maxCost = null)
    {
        var chosen = await SelectCandidate(choiceContext, player, count, maxCost);
        if (chosen != null)
        {
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(chosen);
            await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, player);
        }
        return chosen;
    }

    /// <summary>
    /// 三选一发现（可跳过），仅选择不加入手牌（广阔智慧交换费用用）
    /// </summary>
    public static async Task<CardModel?> SelectCandidate(PlayerChoiceContext choiceContext, Player player, int count = 3, int? maxCost = null)
    {
        var candidates = RollCandidates(player, count, maxCost);
        if (candidates.Count == 0)
        {
            return null;
        }
        return await CardSelectCmd.FromChooseACardScreen(choiceContext, candidates, player, canSkip: true);
    }
}
