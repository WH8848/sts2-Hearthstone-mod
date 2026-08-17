using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 陨石术 (Meteor) - 2费攻击牌（普通，火焰派系）。
/// 对一个敌人造成 15 点伤害，再对随机敌人造成 2 次 4 点伤害。
/// 升级后变为"烈焰风暴 (Flamestrike)"：造成 7 次 5 点伤害，随机分配到所有敌人身上。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MeteorCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 火焰派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Fire];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：陨石术 / 升级后（烈焰风暴）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/flamestrike.png" : "res://assets/card_art/meteor.png";

    public MeteorCard()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"烈焰风暴 (Flamestrike)"
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
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        if (IsUpgraded)
        {
            // 烈焰风暴：造成 7 次 5 点伤害，随机分配到所有敌人
            for (int i = 0; i < 7; i++)
            {
                var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                    .Where(e => e != null && e.IsAlive && e.IsHittable)
                    .ToList();
                if (enemies.Count == 0)
                {
                    break;
                }
                var target = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
                if (target == null)
                {
                    break;
                }
                await CreatureCmd.Damage(choiceContext, [target], 5m, ValueProp.Move, base.Owner.Creature, this, cardPlay);
            }
            return;
        }

        // 陨石术：对一个敌人造成 15 点伤害
        if (cardPlay.Target is { IsAlive: true } mainTarget)
        {
            await CreatureCmd.Damage(choiceContext, [mainTarget], 15m, ValueProp.Move, base.Owner.Creature, this, cardPlay);
        }

        // 再对随机敌人造成 2 次 4 点伤害
        for (int i = 0; i < 2; i++)
        {
            var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                .Where(e => e != null && e.IsAlive && e.IsHittable)
                .ToList();
            if (enemies.Count == 0)
            {
                break;
            }
            var target = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
            if (target == null)
            {
                break;
            }
            await CreatureCmd.Damage(choiceContext, [target], 4m, ValueProp.Move, base.Owner.Creature, this, cardPlay);
        }
    }
}
