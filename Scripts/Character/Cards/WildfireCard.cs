using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 野火 (Wildfire) - 0费技能牌（罕见，火焰派系）。
/// 你的英雄技能造成的伤害增加1点。可无限升级（每次升级额外 +1）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class WildfireCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 无限升级 - 允许无限次升级（每次升级伤害加成 +1）
    /// </summary>
    public override int MaxUpgradeLevel => int.MaxValue;

    /// <summary>
    /// 法术牌 + 火焰派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Fire];

    /// <summary>
    /// 英雄技能伤害加成值（升级预览时显示数值递增，绿色高亮）
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(1m, ValueProp.Move)
    ];

    public override string CustomPortraitPath => "res://assets/card_art/wildfire.png";

    public WildfireCard()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 卡名不变（可无限升级，不改变名称）
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            return title.GetFormattedText();
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 英雄技能伤害 + 加成值（本局永久可叠加）
        int bonus = (int)base.DynamicVars.Damage.BaseValue;
        await PowerCmd.Apply<WildfirePower>(
            choiceContext, [base.Owner.Creature], bonus, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 每次升级加成 +1（UpgradeValueBy 设置 WasJustUpgraded，升级预览数值绿色高亮）
        base.DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
