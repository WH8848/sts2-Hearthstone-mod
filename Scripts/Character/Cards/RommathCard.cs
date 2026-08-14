using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// RommathCard - 吉安娜随从卡。召唤 5/7 的 RommathMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class RommathCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 战吼：再次施放本局施放过的牌库之外的法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry, jaina.Scripts.Character.Keywords.JainaKeywords.Spell, CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/archmage_rommath.png";
    protected override Type MinionType => typeof(RommathMinion);

    protected override int MinionAttack => 5;

    protected override int MinionHealth => 7;

    public RommathCard()
        : base(3, CardRarity.Rare)
    {
    }
}
