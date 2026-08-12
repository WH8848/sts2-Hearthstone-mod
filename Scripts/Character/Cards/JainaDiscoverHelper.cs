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
        // 用 ModelDb 获取 canonical 实例（不能 new，CardPileCmd.Add 会对裸实例重复注册 ModelDb）
        var pool = AttackSkillPool
            .Select(t => MegaCrit.Sts2.Core.Models.ModelDb.GetById<CardModel>(MegaCrit.Sts2.Core.Models.ModelDb.GetId(t)))
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
        var candidates = RollCandidates(player, count, maxCost);
        if (candidates.Count == 0)
        {
            return null;
        }
        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, candidates, player, canSkip: true);
        if (chosen != null)
        {
            await CardPileCmd.Add(chosen, PileType.Hand);
        }
        return chosen;
    }
}
