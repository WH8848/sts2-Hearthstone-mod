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
/// 吉安娜发现（Discover）工具：从吉安娜法术池中随机抽取若干张供玩家选择。
/// 发现池动态构建（BuildAllSpellPool：吉安娜卡池中的法术牌含升级形态，
/// 排除英雄技能/任务线卡与黑名单能力牌）。
/// </summary>
public static class JainaDiscoverHelper
{
    /// <summary>
    /// 吉安娜的礼物（未升级）卡面写明的固定发现池：寒冰箭、奥术智慧、火球术。
    /// 仅吉安娜的礼物（未升级形态）使用；其他发现的池子动态构建。
    /// </summary>
    private static readonly Type[] JainasGiftFixedPool =
    [
        typeof(Frostbolt),
        typeof(ArcaneIntellect),
        typeof(Fireball)
    ];

    /// <summary>
    /// 从发现池中随机选若干张（不重复），可过滤费用上限。
    /// 池：吉安娜卡池（JainaCardPool）中的法术牌（攻击/技能牌，或带"法术牌"关键词的能力牌），
    /// 含升级形态，排除英雄技能卡、任务卡（带 Quest 关键词：禁忌序列/打开时空之门/巫师的计策/
    /// 拖延时间/抵达传送大厅）与黑名单能力牌（戏法图腾/炉石形态）。
    /// 每种法术牌按可升级级别展开。
    /// </summary>
    public static List<CardModel> RollCandidates(Player player, int count = 3, int? maxCost = null)
    {
        var combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return [];
        }
        // 动态构建候选池：吉安娜卡池中的法术牌（类型 + 升级级别一起展开）
        var pool = new List<CardModel>();
        foreach (var canonical in MegaCrit.Sts2.Core.Models.ModelDb.CardPool<JainaCardPool>().AllCards)
        {
            if (canonical == null)
            {
                continue;
            }
            // 法术牌 = 攻击/技能牌，或带"法术牌"关键词的卡（Power 型能力牌）
            bool isSpellCard = canonical.Type == CardType.Attack || canonical.Type == CardType.Skill ||
                               canonical.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Spell) == true;
            if (!isSpellCard)
            {
                continue;
            }
            // 显式黑名单：戏法图腾/炉石形态不是法术牌；禁忌序列/打开时空之门任务卡不可被发现
            if (jaina.Scripts.Character.JainaCastTracker.IsExcludedFromSpellPool(canonical.GetType()))
            {
                continue;
            }
            // 英雄技能卡（火焰冲击等）不可被发现
            if (jaina.Scripts.Character.Powers.HeroPowerHandHelper.IsHeroPowerCard(canonical))
            {
                continue;
            }
            // 任务线卡不可被发现
            if (canonical.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Quest) == true)
            {
                continue;
            }
            int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(canonical.GetType());
            for (int level = 0; level <= maxLevel; level++)
            {
                var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                    combatState, player, canonical.GetType(), level);
                if (card != null)
                {
                    pool.Add(card);
                }
            }
        }
        if (maxCost is int max && max >= 0)
        {
            // 用当前基础费用（含升级减费，不含临时修正）：升级后减费到 <=max 的形态也会入选
            pool = pool.Where(c => c.EnergyCost.GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers.None) <= max).ToList();
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
    /// 吉安娜的礼物（未升级）专用：三选一发现（卡面写明寒冰箭/奥术智慧/火球术，均带虚无）。
    /// 从固定三张池中生成候选（不含升级形态），选中的牌加入手牌。
    /// </summary>
    public static async Task<CardModel?> DiscoverJainasGift(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = player?.Creature?.CombatState;
        if (combatState == null)
        {
            return null;
        }
        var pool = new List<CardModel>();
        foreach (var t in JainasGiftFixedPool)
        {
            var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(t));
            if (canonical == null)
            {
                continue;
            }
            // 附加虚无：回合结束时留在手牌则消耗
            var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, player, t, 0);
            if (card != null)
            {
                MegaCrit.Sts2.Core.Commands.CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);
                pool.Add(card);
            }
        }
        if (pool.Count == 0)
        {
            return null;
        }
        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, pool.AsReadOnly(), player, canSkip: true);
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
    /// 发现一张火焰/冰霜/奥术派系法术牌（任务线阶段 2 奖励用）：
    /// 三派系动态池合并（BuildSchoolSpellPool × Fire/Frost/Arcane），
    /// 三选一，选中的牌加入手牌。
    /// </summary>
    public static async Task<CardModel?> DiscoverSchoolSpellAndAddToHand(
        PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = player?.Creature?.CombatState;
        if (combatState == null)
        {
            return null;
        }
        // 合并火焰/冰霜/奥术三派系的动态池（类型+升级级别）
        var entries = new List<(System.Type Type, int UpgradeLevel)>();
        entries.AddRange(jaina.Scripts.Character.JainaCastTracker.BuildSchoolSpellPool(
            combatState, player, jaina.Scripts.Character.JainaSpellSchool.Fire));
        entries.AddRange(jaina.Scripts.Character.JainaCastTracker.BuildSchoolSpellPool(
            combatState, player, jaina.Scripts.Character.JainaSpellSchool.Frost));
        entries.AddRange(jaina.Scripts.Character.JainaCastTracker.BuildSchoolSpellPool(
            combatState, player, jaina.Scripts.Character.JainaSpellSchool.Arcane));
        if (entries.Count == 0)
        {
            return null;
        }
        // 随机三选一（不重复）
        var rng = player.RunState.Rng.CombatTargets;
        var pool = new List<(System.Type Type, int UpgradeLevel)>(entries);
        var candidates = new List<CardModel>();
        while (candidates.Count < 3 && pool.Count > 0)
        {
            var entry = rng.NextItem(pool);
            if (entry.Type == null)
            {
                break;
            }
            pool.Remove(entry);
            var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, player, entry.Type, entry.UpgradeLevel);
            if (card != null)
            {
                candidates.Add(card);
            }
        }
        if (candidates.Count == 0)
        {
            return null;
        }
        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, candidates, player, canSkip: true);
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
    /// 发现一张"费用消耗精确等于指定值"的卡牌（拾荒清道夫/冬泉雏龙战吼用）。
    /// 池：默认 <paramref name="allClasses"/>=false 时取 JainaCardPool 全部卡
    /// （法术/随从/地标）；<paramref name="allClasses"/>=true 时取<b>任意角色</b>全部卡
    /// （ModelDb.AllCards，应用 Jaina 随机池统一排除：非角色池/任务卡/先古稀有度/多人专属）。
    /// 每种按可升级级别展开；排除英雄技能卡（火焰冲击等）、英雄卡（魔导师晨拥）与任务线卡；
    /// X 费卡（禁忌烈焰/禁忌神龛）费用不定，不管费用多少总是作为候选。
    /// 同名卡不可自发现：排除 <paramref name="excludeType"/>（发起发现的卡自身）。
    /// 费用过滤用当前基础费用（GetWithModifiers(None)，含升级减费）。
    /// </summary>
    public static async Task<CardModel?> DiscoverCardOfCostAndAddToHand(
        PlayerChoiceContext choiceContext, Player player, int cost, System.Type? excludeType = null,
        bool allClasses = false)
    {
        if (player?.Creature?.CombatState == null)
        {
            return null;
        }
        var combatState = player.Creature.CombatState;
        var pool = new List<CardModel>();

        void AddCandidates(Type cardType)
        {
            // 同名卡不可自发现：排除发起发现的卡自身
            if (excludeType != null && cardType == excludeType)
            {
                return;
            }
            var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(cardType));
            if (canonical == null)
            {
                return;
            }
            // 任意角色模式：应用 Jaina 随机池统一排除
            // （8 个非角色/衍生池/任务卡/先古稀有度/多人专属——见 JainaRandomPoolHelper.IsEligible）
            if (allClasses && !jaina.Scripts.Character.JainaRandomPoolHelper.IsEligible(canonical))
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
                // 费用过滤用当前基础费用（含升级减费）：升级后减费的形态按实际费用匹配，
                // 不会以未升级的纸面费用混入更高费用的发现池（如拾荒清道夫按剩余费用发现）
                if (card != null && (isXCost ||
                    card.EnergyCost.GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers.None) == cost))
                {
                    pool.Add(card);
                }
            }
        }

        // 卡池：默认吉安娜主卡池；任意角色模式用全部卡（ModelDb.AllCards）
        var sourcePool = allClasses
            ? MegaCrit.Sts2.Core.Models.ModelDb.AllCards
            : ModelDb.CardPool<JainaCardPool>().AllCards;
        foreach (var canonical in sourcePool)
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
