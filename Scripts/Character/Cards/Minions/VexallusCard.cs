using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 维萨鲁斯 (Vexallus) - 2费随从卡（稀有，元素）。
/// 你的奥术法术会施放两次。属性 3/5。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class VexallusCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"维萨鲁斯"（Vexallus, MLK_84390）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/vexallus.png";

    protected override Type MinionType => typeof(VexallusMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 5;

    /// <summary>
    /// 元素种族；随从卡打出后消耗（模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Elemental, CardKeyword.Exhaust];

    public VexallusCard()
        : base(2, CardRarity.Rare)
    {
    }
}
