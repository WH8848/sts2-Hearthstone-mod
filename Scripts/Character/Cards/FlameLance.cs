using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 炎枪术 (Flame Lance) - 2费攻击（普通，火焰派系）。
/// 对一个敌人造成 25 点伤害。
/// 升级后变为"陨石术 (Meteor)"：对一个敌人造成 15 点伤害，并对随机敌人造成 2 次 4 点伤害。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class FlameLance : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 火焰派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Fire];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(25m, ValueProp.Move)
    ];

    /// <summary>
    /// 卡牌原画：炎枪术 / 升级后（陨石术）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/meteor.png" : "res://assets/card_art/flame_lance.png";

    public FlameLance()
        : base(2, CardType.Attack, CardRarity.Common, JainaTargetTypes.AnyTargetable, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"陨石术 (Meteor)"
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

        if (cardPlay.Target is not { IsAlive: true } target)
        {
            return;
        }

        if (IsUpgraded)
        {
            // 陨石术：对目标造成 15 点伤害（吃力量修正）
            await DamageCmd.Attack(15m)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);

            // 并对随机敌人造成 2 次 4 点伤害
            var combatState = base.Owner.Creature.CombatState;
            for (int i = 0; i < 2; i++)
            {
                var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                    .Where(e => e.IsAlive && e.IsHittable)
                    .ToList();
                if (enemies.Count == 0)
                {
                    break;
                }
                var randomTarget = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
                if (randomTarget == null)
                {
                    break;
                }
                await CreatureCmd.Damage(choiceContext, [randomTarget], 4m, ValueProp.Unpowered, base.Owner.Creature);
            }
        }
        else
        {
            // 炎枪术：对目标造成 25 点伤害（吃力量修正）
            await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级为陨石术：主伤害 25 -> 15（另含随机 2 次 × 4 点）
        base.DynamicVars.Damage.BaseValue = 15m;
    }
}
