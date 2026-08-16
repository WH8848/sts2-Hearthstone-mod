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
/// 陨石术 (Meteor) - 2费攻击牌（稀有，火焰派系）。
/// 对一个敌人造成 15 点伤害，再随机造成 2 次 4 点伤害。
/// 升级后变为"星辰能量 (Star Power)"：随机对一个敌方造成 5 点伤害。
/// 重复此效果，每次伤害减少 1 点（5、4、3、2、1）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MeteorCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 火焰派系（升级后为奥术派系）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded
            ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane]
            : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Fire];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：陨石术 / 升级后（星辰能量）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/star_power.png" : "res://assets/card_art/meteor.png";

    public MeteorCard()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"星辰能量 (Star Power)"
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
            // 星辰能量：随机对一个敌方造成 5 点伤害，重复此效果每次伤害减少 1 点（5、4、3、2、1）
            for (int damage = 5; damage >= 1; damage--)
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
                await CreatureCmd.Damage(choiceContext, [target], damage, ValueProp.Move, base.Owner.Creature, this, cardPlay);
            }
            return;
        }

        // 陨石术：对一个敌人造成 15 点伤害
        if (cardPlay.Target is { IsAlive: true } mainTarget)
        {
            await CreatureCmd.Damage(choiceContext, [mainTarget], 15m, ValueProp.Move, base.Owner.Creature, this, cardPlay);
        }

        // 再随机造成 2 次 4 点伤害
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
