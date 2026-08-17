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
/// 发现池：火球术、寒冰箭、火焰冲击、奥术智慧、霜冻射线、寒冰护盾。
/// </summary>
public static class JainaDiscoverHelper
{
    /// <summary>
    /// 吉安娜攻击/技能牌池（可被发现）。
    /// 注意：火焰冲击是英雄技能，不可被衍生发现。
    /// </summary>
    private static readonly Type[] AttackSkillPool =
    [
        typeof(Fireball),
        typeof(Frostbolt),
        typeof(ArcaneIntellect),
        typeof(RayOfFrostCard),
        typeof(IceBarrier)
    ];

    /// <summary>
    /// 从发现池中随机选若干张（不重复），可过滤费用上限。
    /// 每种法术牌按可升级级别展开：未升级形态与全部升级形态（+）都可被发现。
    /// </summary>
    public static List<CardModel> RollCandidates(Player player, int count = 3, int? maxCost = null)
    {
        // 用 CreateCard 生成带 Owner 的实例（MutableClone 的卡无 Owner，AddGeneratedCardToCombat 会 NRE）
        var combatState = player.Creature.CombatState;
        var pool = new List<CardModel>();
        foreach (var t in AttackSkillPool)
        {
            var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(t));
            if (canonical == null)
            {
                continue;
            }
            // 展开升级形态：未升级 + 允许的升级级别（点燃只能未升级形态）
            int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(t);
            for (int level = 0; level <= maxLevel; level++)
            {
                var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                    combatState, player, t, level);
                if (card != null)
                {
                    pool.Add(card);
                }
            }
        }
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
    /// 三选一发现（可跳过），选中的牌加入手牌。
    /// 手牌满时不入手（0.111.1 满手时 Add 会把牌静默改道弃牌堆）。
    /// </summary>
    public static async Task<CardModel?> DiscoverAndAddToHand(PlayerChoiceContext choiceContext, Player player, int count = 3, int? maxCost = null)
    {
        var chosen = await SelectCandidate(choiceContext, player, count, maxCost);
        if (chosen != null)
        {
            if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
            {
                return null;
            }
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

    /// <summary>
    /// 从吉安娜全卡池中发现一张"费用消耗精确等于指定值"的卡牌（拾荒清道夫战吼用）。
    /// 池：JainaCardPool 全部卡（法术/随从/地标），每种按可升级级别展开；
    /// 排除英雄技能卡（火焰冲击等）、英雄卡（魔导师晨拥）与任务线卡（不可被发现）；
    /// X 费卡（禁忌烈焰/禁忌神龛）费用不定，不管剩余费用多少总是作为候选。
    /// </summary>
    public static async Task<CardModel?> DiscoverCardOfCostAndAddToHand(
        PlayerChoiceContext choiceContext, Player player, int cost)
    {
        if (player?.Creature?.CombatState == null)
        {
            return null;
        }
        var combatState = player.Creature.CombatState;
        var pool = new List<CardModel>();

        void AddCandidates(Type cardType)
        {
            var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(cardType));
            if (canonical == null)
            {
                return;
            }
            // 英雄技能卡、英雄卡与任务线卡不可被发现
            if (canonical.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower) == true ||
                canonical.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Quest) == true ||
                canonical.Type == JainaCardTypes.Hero)
            {
                return;
            }
            // X 费卡（CostsX）费用不定：不管剩余费用多少，总是作为候选出现在发现池里
            bool isXCost = canonical.EnergyCost.CostsX;
            // 展开升级形态（未升级 + 允许的升级级别）
            int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(cardType);
            for (int level = 0; level <= maxLevel; level++)
            {
                var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                    combatState, player, cardType, level);
                if (card != null && (isXCost || card.EnergyCost.Canonical == cost))
                {
                    pool.Add(card);
                }
            }
        }

        // 吉安娜主卡池全部卡
        foreach (var canonical in ModelDb.CardPool<JainaCardPool>().AllCards)
        {
            if (canonical != null)
            {
                AddCandidates(canonical.GetType());
            }
        }

        if (pool.Count == 0)
        {
            return null;
        }
        // 随机三选一（不足 3 张时全给）
        var picked = new List<CardModel>();
        while (picked.Count < 3 && pool.Count > 0)
        {
            var card = player.RunState.Rng.CombatTargets.NextItem(pool);
            if (card == null)
            {
                break;
            }
            picked.Add(card);
            pool.Remove(card);
        }
        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, picked, player, canSkip: true);
        if (chosen != null)
        {
            if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
            {
                return null;
            }
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(chosen);
            await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, player);
        }
        return chosen;
    }
}
