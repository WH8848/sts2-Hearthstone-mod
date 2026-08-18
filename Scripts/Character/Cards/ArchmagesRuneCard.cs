using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Powers;
using MinionLib.Targeting;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 大法师的符文 (Archmage's Rune) - 3费技能牌（普通）。
/// 对敌人施放消耗总计6费的法师法术。可无限升级，每次升级多1费。消耗。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ArchmagesRuneCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 无限升级 - 允许无限次升级（每次升级总费用 +1）
    /// </summary>
    public override int MaxUpgradeLevel => int.MaxValue;

    /// <summary>
    /// 法术牌 + 消耗（打出后从本场战斗移除）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Spell, CardKeyword.Exhaust];

    /// <summary>
    /// 施放法术的总费用（基础 6，升级 +1/级）
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("TotalCost", 6)
    ];

    public override string CustomPortraitPath => "res://assets/card_art/archmages_rune.png";

    public ArchmagesRuneCard()
        : base(3, CardType.Skill, CardRarity.Common, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级：每次升级多1费（UpgradeValueBy 设置 WasJustUpgraded，升级预览数值绿色高亮）
    /// </summary>
    protected override void OnUpgrade()
    {
        if (base.DynamicVars.TryGet<IntVar>("TotalCost", out var cost))
        {
            cost.UpgradeValueBy(1m);
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var owner = base.Owner;
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rng = owner.RunState.Rng.CombatTargets;

        // 法师法术池：吉安娜卡池中的攻击/技能牌（不含英雄技能卡），按可升级级别展开
        var pool = new List<CardModel>();
        foreach (var canonical in ModelDb.CardPool<JainaCardPool>().AllCards)
        {
            if (canonical == null)
            {
                continue;
            }
            if (canonical.Type != CardType.Attack && canonical.Type != CardType.Skill)
            {
                continue;
            }
            if (HeroPowerHandHelper.IsHeroPowerCard(canonical))
            {
                continue;
            }
            int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(canonical.GetType());
            for (int level = 0; level <= maxLevel; level++)
            {
                var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                    combatState, owner, canonical.GetType(), level);
                if (card != null)
                {
                    pool.Add(card);
                }
            }
        }

        // 随机施放法术直到总费用耗尽（只选费用 <= 剩余预算的正费用法术；0费法术不贡献跳过）
        int budget = base.DynamicVars.GetIntOrDefault("TotalCost", 6);
        while (budget > 0)
        {
            var affordable = pool
                .Where(c => c.EnergyCost.GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers.None) > 0 &&
                            c.EnergyCost.GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers.None) <= budget)
                .ToList();
            if (affordable.Count == 0)
            {
                break;
            }
            var card = rng.NextItem(affordable);
            if (card == null)
            {
                break;
            }

            // 目标：敌人（单目标法术从场上所有敌人中随机选合法目标）
            Creature? target = null;
            if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyPlayer ||
                card.TargetType == TargetType.AnyAlly ||
                (CustomTargetTypeManager.TryGetCustomTargetType(card.TargetType, out var customType) &&
                 customType.IsSingleTarget))
            {
                var enemies = combatState.Creatures
                    .Where(c => c != null && c.IsAlive && c.Side != owner.Creature.Side && card.IsValidTarget(c))
                    .ToList();
                if (enemies.Count == 0)
                {
                    pool.Remove(card);
                    continue; // 无合法敌人目标：跳过此牌（不扣预算）
                }
                target = rng.NextItem(enemies);
            }

            pool.Remove(card);
            budget -= (int)card.EnergyCost.GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers.None);
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
            jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(card);
            await CardCmd.AutoPlay(choiceContext, card, target);
        }
    }
}
