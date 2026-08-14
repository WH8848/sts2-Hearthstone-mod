using System.Collections.Generic;
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
/// 火球术 (Fireball) - 吉安娜的基础攻击牌。
/// 1费造成6点伤害，升级后变为炎爆术 (Pyroblast)，造成10点伤害。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterCharacterStarterCard(typeof(Jaina), 5)]
public sealed class Fireball : JainaSpellCardTemplate
{
    protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };

    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Fire];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move)
    ];

    /// <summary>
    /// 卡牌原画：炉石传说"火球术"高清原画。
    /// 升级后变为"炎爆术"，卡图同步切换为炎爆术原画。
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded
            ? "res://assets/card_art/pyroblast.png"
            : "res://assets/card_art/fireball.png";

    public Fireball()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"炎爆术 (Pyroblast)"
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

        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // 伤害 6 -> 10
        base.DynamicVars.Damage.BaseValue = 10m;
    }
}
