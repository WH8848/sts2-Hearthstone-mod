using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 奥术弹幕 (Arcane Barrage) - 1费攻击（普通，奥术派系）。
/// 对一个敌人造成 3 点伤害，再随机对所有敌人造成 2 次 2 点伤害。
/// 升级后变为"灯光表演 (Lightshow)"：对随机敌人造成 2 次 2 点伤害，
/// 同名牌每释放 1 次次数 +1（光束数随本局施放次数递增）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ArcaneBarrage : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 奥术派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move)
    ];

    /// <summary>
    /// 卡牌原画：奥术弹幕 / 升级后（灯光表演）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/lightshow.png" : "res://assets/card_art/arcane_barrage.png";

    public ArcaneBarrage()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"灯光表演 (Lightshow)"
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

    /// <summary>
    /// 灯光表演光束数：2 + 本局已施放过的灯光表演次数（施放本张前计数）
    /// </summary>
    private int LightshowBeamCount(ICombatState combatState)
    {
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        return 2 + rec.LightshowCasts;
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
            // 灯光表演：对随机敌人造成 N 次 2 点伤害（N = 2 + 本局已施放次数）
            int beams = LightshowBeamCount(combatState);
            // 施放本张后计数 +1（供下一次灯光表演递增）
            jaina.Scripts.Character.JainaCastTracker.For(combatState).LightshowCasts++;
            for (int i = 0; i < beams; i++)
            {
                var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                    .Where(e => e.IsAlive && e.IsHittable)
                    .ToList();
                if (enemies.Count == 0)
                {
                    break;
                }
                var randomEnemy = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
                if (randomEnemy == null)
                {
                    break;
                }
                await CreatureCmd.Damage(choiceContext, [randomEnemy], 2m, ValueProp.Move, base.Owner.Creature, this, cardPlay);
            }
            return;
        }

        // 奥术弹幕：对目标造成 {Damage} 点伤害（吃力量修正）
        if (cardPlay.Target is not { IsAlive: true } target)
        {
            return;
        }
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        // 再随机对所有敌人造成 2 次 2 点伤害
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
            // Move 标记：每段伤害都触发振翅（Flutter）层数减少（IsPoweredAttack）；
            // 传 cardSource/cardPlay（蜷身等依赖 cardSource 的敌方 Power 才能触发）
            await CreatureCmd.Damage(choiceContext, [randomTarget], 2m, ValueProp.Move, base.Owner.Creature, this, cardPlay);
        }
    }
}
