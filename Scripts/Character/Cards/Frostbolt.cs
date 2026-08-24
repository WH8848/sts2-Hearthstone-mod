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
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 寒冰箭 (Frostbolt) - 0费：对一个角色造成 3 点伤害，并使其获得 1 层冻结。
/// 升级后变为冰锥术 (Cone of Cold)：随机对敌人造成 1 点伤害 3 次（吃力量，
/// 每击 = 1 + 力量），每次伤害都会给予被击中的敌人 1 层冻结（无需选择目标，冰霜派系）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterCharacterStarterCard(typeof(Jaina), 1, Order = 3)]
public sealed class Frostbolt : JainaSpellCardTemplate
{
    /// <summary>
    /// 只能升级 1 次（升级变为冰锥术）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Freeze, jaina.Scripts.Character.Keywords.JainaKeywords.Frost];

    /// <summary>
    /// 动态伤害显示：未升级 = 3(基础,预览含力量等修正)；升级(冰锥术) = 1(每击基础,
    /// 吃力量加成——每击 = 1 + 力量,预览含力量)。
    /// (分支声明:CanonicalVars 不会为升级形态重新求值,与陨石术/模拟残像同模式)
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        IsUpgraded
            ? [new DamageVar(1m, ValueProp.Move)]
            : [new DamageVar(3m, ValueProp.Move)];

    /// <summary>
    /// 升级后（冰锥术）无需选择目标：随机对敌人造成 1 点伤害 3 次（吃力量，每击 = 1 + 力量）
    /// </summary>
    public override TargetType TargetType =>
        IsUpgraded ? TargetType.None : JainaTargetTypes.AnyTargetable;

    /// <summary>
    /// 卡牌原画：寒冰箭 / 升级后（冰锥术）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/cone_of_cold.png" : "res://assets/card_art/frostbolt.png";

    public Frostbolt()
        : base(0, CardType.Attack, CardRarity.Basic, JainaTargetTypes.AnyTargetable, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"冰锥术 (Cone of Cold)"
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

        // 冰锥术（升级后）：随机对敌人造成 1 点伤害 3 次（吃力量——每击 = 1 + 力量），
        // 每次伤害给予被击中的敌人 1 层冻结
        if (IsUpgraded)
        {
            var combatState = base.Owner.Creature.CombatState;
            if (combatState == null)
            {
                return;
            }
            for (int i = 0; i < 3; i++)
            {
                var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                    .Where(e => e != null && e.IsAlive && e.IsHittable)
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
                // 冰锥术：每击 1 点伤害基数 ×3 次（Powered 默认吃力量加成，每击 = 1 + 力量；
                // 卡面 {Damage:diff()} 同源动态显示，与 GreaterArcaneMissiles 同模式）
                await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(randomTarget)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .Execute(choiceContext);
                // 每次伤害给予被命中的敌人 1 层冻结（目标已死则不需要挂）
                if (randomTarget.IsAlive)
                {
                    await PowerCmd.Apply<FreezePower>(choiceContext, [randomTarget], 1m, base.Owner.Creature, this);
                }
            }
            return;
        }

        if (cardPlay.Target is not { IsAlive: true } target)
        {
            return;
        }

        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        // 给予 1 层冻结
        await PowerCmd.Apply<FreezePower>(choiceContext, [target], 1m, base.Owner.Creature, this);
    }

    // 升级为冰锥术：不再升级基础伤害（冰锥术每击基础 1 点×3 次，吃力量加成
    // ——伤害构成差异由卡面 {Damage:diff()} 动态描述体现）
}
