using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// MozakiCard - 吉安娜随从卡。召唤 3/8 的 MozakiMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MozakiCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 描述中提及"法术牌"，挂法术牌关键词以提供右侧悬停解释（不注入卡面）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/mozaki.png";
    protected override Type MinionType => typeof(MozakiMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 8;

    public MozakiCard()
        : base(2, CardRarity.Rare)
    {
    }
}
