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
using jaina.Scripts.Character.Powers;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 禁忌神龛 (Forbidden Shrine) - X费技能牌（罕见）。
/// 随机施放一个 x 费法术（x = 消耗的能量，上限 3 费）。
/// 升级后（禁忌神龛+）：随机施放一个 x+1 费法术（上限 3 费）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ForbiddenShrineCard : JainaSpellCardTemplate
{
    /// <summary>
    /// X 费用：自动消耗玩家全部剩余能量
    /// </summary>
    protected override bool HasEnergyCostX => true;

    /// <summary>
    /// 法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/forbidden_shrine.png";

    public ForbiddenShrineCard()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"禁忌神龛+"
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            return title.GetFormattedText() + "+";
        }
    }

    /// <summary>
    /// 吉安娜全部法术牌池（随机施放用）：动态构建（攻击/技能牌，含升级形态，
    /// 排除英雄技能卡、任务线卡与自身——BuildAllSpellPool 后过滤自身）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // x = 消耗的能量（上限 3）；升级后目标费用 x+1（上限 3）
        int x = ResolveEnergyXValue();
        int targetCost = IsUpgraded ? x + 1 : x;
        targetCost = System.Math.Min(targetCost, 3);

        // 收集原始费用 = targetCost 的吉安娜法术牌（含升级形态），排除自身（同名不可自发现）
        var pool = new List<CardModel>();
        foreach (var (type, level) in jaina.Scripts.Character.JainaCastTracker.BuildAllSpellPool())
        {
            if (type == typeof(ForbiddenShrineCard))
            {
                continue;
            }
            var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, base.Owner, type, level);
            if (card == null)
            {
                continue;
            }
            // 费用匹配用当前基础费用（含升级减费）：升级后减费到目标费用的形态也入选
            if (card.EnergyCost.GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers.None) != targetCost)
            {
                continue;
            }
            pool.Add(card);
        }
        if (pool.Count == 0)
        {
            return;
        }
        var chosen = base.Owner.RunState.Rng.CombatCardSelection.NextItem(pool);
        if (chosen == null)
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(chosen);

        // 单目标法术：随机选合法目标（尽可能以敌人为目标——有合法敌人只打敌人；
        // 无敌人时回退全部合法目标，不放弃施放，与魔法智慧之球统一语义）
        Creature? target = null;
        if (chosen.TargetType == TargetType.AnyEnemy || chosen.TargetType == TargetType.AnyPlayer ||
            chosen.TargetType == TargetType.AnyAlly ||
            (CustomTargetTypeManager.TryGetCustomTargetType(chosen.TargetType, out var customType) &&
             customType.IsSingleTarget))
        {
            IEnumerable<Creature> targetPool = combatState.Creatures
                .Where(c => c != null && c.IsAlive && chosen.IsValidTarget(c));
            var enemies = targetPool.Where(c => c.Side != base.Owner.Creature.Side).ToList();
            if (enemies.Count > 0)
            {
                targetPool = enemies;
            }
            var targets = targetPool.ToList();
            target = targets.Count > 0 ? base.Owner.RunState.Rng.CombatTargets.NextItem(targets) : null;
            if (target == null)
            {
                return;
            }
        }
        await CardCmd.AutoPlay(choiceContext, chosen, target);
    }
}
