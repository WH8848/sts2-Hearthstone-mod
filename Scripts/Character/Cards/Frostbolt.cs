using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 寒冰箭 (Frostbolt) - 0费普通攻击牌（冰霜派系）。
/// 对一个角色造成 3 点伤害，并使其获得 1 层冻结。
/// 升级后变为冰枪术 (Icy Lance)：给予一个角色 1 层冻结，造成 0 点基础伤害；
/// 该角色身上每有一层冻结，就对其额外造成 4 点伤害（按施放后层数计算，需选择目标）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class Frostbolt : JainaSpellCardTemplate
{
    /// <summary>
    /// 只能升级 1 次（升级变为冰枪术）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Freeze, jaina.Scripts.Character.Keywords.JainaKeywords.Frost];

    /// <summary>
    /// 伤害变量（单一 Computed + 目标感知——分支声明(IsUpgraded ? ... : ...)不会为
    /// 升级形态重新求值,改用 CurrentUpgradeLevel 分支):
    /// 未升级 = 3(预览含力量等修正)；升级(冰枪术) = (目标当前冻结层数 + 本卡 1 层) × 4
    /// ——卡面按施放后层数显示(3 层敌人 → 16；0 层 → 4；无目标 → 0)。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ComputedDamageVar(3m, (card, target) =>
        {
            if (card is not Frostbolt f || f.CurrentUpgradeLevel < 1)
            {
                return 3m;
            }
            // 冰枪术：0 + (目标已有冻结层数 + 本卡 1 层) × 4（target 感知；无目标 → 0）
            var freeze = target?.GetPower<FreezePower>();
            return ((freeze?.Amount ?? 0m) + 1m) * 4m;
        })
    ];

    /// <summary>
    /// 升级后（冰枪术）选择目标；基础（寒冰箭）选择角色
    /// </summary>
    public override TargetType TargetType => JainaTargetTypes.AnyTargetable;

    /// <summary>
    /// 卡牌原画：寒冰箭 / 升级后（冰枪术）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/ice_lance.png" : "res://assets/card_art/frostbolt.png";

    public Frostbolt()
        : base(0, CardType.Attack, CardRarity.Common, JainaTargetTypes.AnyTargetable, true)
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

        // 冰枪术（升级后）：给予一个角色 1 层冻结，造成 0 点基础伤害；
        // 该角色身上每有一层冻结，额外造成 4 点伤害——
        // 按施放后的层数计算（先叠 1 层，再结算：3 层敌人→叠至 4 层→16 点；
        // 卡面预览同口径：显示 (当前层数 + 1) × 4）
        if (IsUpgraded)
        {
            if (cardPlay.Target is not { IsAlive: true } icyTarget)
            {
                return;
            }
            await PowerCmd.Apply<FreezePower>(choiceContext, [icyTarget], 1m, base.Owner.Creature, this);
            var afterFreeze = icyTarget.GetPower<FreezePower>();
            var stacks = afterFreeze?.Amount ?? 0m;
            if (stacks > 0)
            {
                await DamageCmd.Attack(stacks * 4m)
                    .FromCard(this, cardPlay)
                    .Targeting(icyTarget)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .Execute(choiceContext);
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

    // 升级为冰枪术：不再升级基础伤害（冰枪术 = 0 基础 + 施放后冻结层数 × 4，
    // 卡面 {Damage:diff()} 目标感知动态显示）
}
