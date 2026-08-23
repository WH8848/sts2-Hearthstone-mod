using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 冰龙吐息 (Frost Dragon Breath) - 0费攻击牌（普通，冰霜派系）。
/// 随机对一个敌人造成 2 点伤害，并给予 1 层冻结。
/// 升级后变为冰枪术 (Icy Lance)：给予一个角色 1 层冻结，
/// 若该角色已拥有冻结，则每层冻结对其额外造成 4 点伤害（需选择目标）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class FrostDragonBreathCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 只能升级 1 次（升级变为冰枪术）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Freeze, jaina.Scripts.Character.Keywords.JainaKeywords.Frost];

    /// <summary>
    /// 动态伤害显示：未升级 = 2(基础,预览含力量等修正)；升级(冰枪术) = 4(每层,预览含力量)。
    /// 分支声明(CanonicalVars 不会为升级形态重新求值,同陨石术模式)。
    /// </summary>
    protected override IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars =>
        IsUpgraded
            ? [new MegaCrit.Sts2.Core.Localization.DynamicVars.DamageVar(4m, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move)]
            : [new MegaCrit.Sts2.Core.Localization.DynamicVars.DamageVar(2m, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move)];

    /// <summary>
    /// 升级后（冰枪术）需要选择目标；未升级（冰龙吐息）随机打敌人，无需选目标
    /// </summary>
    public override TargetType TargetType =>
        IsUpgraded ? JainaTargetTypes.AnyTargetable : TargetType.None;

    /// <summary>
    /// 卡牌原画：冰龙吐息（炉石原卡 Breath of Sindragosa 原画，取自 hearthstone.wiki.gg）/
    /// 升级后（冰枪术）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/ice_lance.png" : "res://assets/card_art/breath_of_sindragosa.png";

    public FrostDragonBreathCard()
        : base(0, CardType.Attack, CardRarity.Common, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"冰枪术 (Icy Lance)"
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

        // 冰枪术（升级后）：给予一个角色 1 层冻结；若已有冻结，每层冻结额外造成 4 点伤害
        // （伤害按施放前已有层数计算，与炉石冰枪术一致；随后再挂 1 层）
        if (IsUpgraded)
        {
            if (cardPlay.Target is not { IsAlive: true } icyTarget)
            {
                return;
            }
            var existingFreeze = icyTarget.GetPower<FreezePower>();
            if (existingFreeze != null && existingFreeze.Amount > 0)
            {
                await DamageCmd.Attack(existingFreeze.Amount * 4m)
                    .FromCard(this, cardPlay)
                    .Targeting(icyTarget)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .Execute(choiceContext);
            }
            await PowerCmd.Apply<FreezePower>(choiceContext, [icyTarget], 1m, base.Owner.Creature, this);
            return;
        }

        // 冰龙吐息（未升级）：随机对一个敌人造成 2 点伤害，并给予 1 层冻结
        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
            .Where(e => e != null && e.IsAlive && e.IsHittable)
            .ToList();
        if (enemies.Count == 0)
        {
            return;
        }
        var target = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target == null)
        {
            return;
        }
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
        // 给予被命中的敌人 1 层冻结（目标已死则无需挂）
        if (target.IsAlive)
        {
            await PowerCmd.Apply<FreezePower>(choiceContext, [target], 1m, base.Owner.Creature, this);
        }
    }
}
