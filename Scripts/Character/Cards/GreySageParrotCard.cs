using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 灰贤鹦鹉 (Grey Sage Parrot) - 吉安娜随从卡。召唤 4/5 的 GreySageParrotMinion。
/// 战吼：重复你施放的上一个费用消耗大于等于 2 点的法术。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class GreySageParrotCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 野兽种族 + 战吼（悬停解释）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Beast, jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry,
         CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/grey_sage_parrot.png";
    protected override Type MinionType => typeof(GreySageParrotMinion);

    protected override int MinionAttack => 4;

    protected override int MinionHealth => 5;

    public GreySageParrotCard()
        : base(2, CardRarity.Uncommon)
    {
    }
}
