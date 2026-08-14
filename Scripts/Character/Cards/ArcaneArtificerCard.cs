using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// ArcaneArtificerCard - 吉安娜随从卡。召唤 1/3 的 ArcaneArtificerMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ArcaneArtificerCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 描述中提及"法术牌"，挂法术牌关键词以提供右侧悬停解释（不注入卡面）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/arcane_artificer.png";
    protected override Type MinionType => typeof(ArcaneArtificerMinion);

    protected override int MinionAttack => 1;

    protected override int MinionHealth => 3;

    public ArcaneArtificerCard()
        : base(0, CardRarity.Common)
    {
    }
}
