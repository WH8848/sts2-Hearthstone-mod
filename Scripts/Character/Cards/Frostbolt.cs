using System.Collections.Generic;
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
/// 升级后变为冰枪术 (Icy Lance)：给予一个角色 1 层冻结，如果该角色已拥有冻结，
/// 则每层冻结对其额外造成 4 点伤害。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class Frostbolt : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Freeze, jaina.Scripts.Character.Keywords.JainaKeywords.Frost];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move)
    ];

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

        if (cardPlay.Target is not { IsAlive: true } target)
        {
            return;
        }

        // 冰枪术（升级后）：不造成基础伤害，若目标已有冻结，每层冻结额外造成 4 点伤害
        if (IsUpgraded)
        {
            var existingFreeze = target.GetPower<FreezePower>();
            if (existingFreeze != null && existingFreeze.Amount > 0)
            {
                await DamageCmd.Attack(existingFreeze.Amount * 4m)
                    .FromCard(this, cardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_blunt")
                    .Execute(choiceContext);
            }
        }
        else
        {
            await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }

        // 给予 1 层冻结
        await PowerCmd.Apply<FreezePower>(choiceContext, [target], 1m, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级为冰枪术：改为冻结+额外伤害效果（基础伤害不再生效，3 -> 0）
        base.DynamicVars.Damage.UpgradeValueBy(-3m);
    }
}
