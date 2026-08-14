using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// AntonidasCard - 吉安娜随从卡。召唤 5/7 的 AntonidasMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class AntonidasCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 描述中提及"法术牌"，挂法术牌关键词以提供右侧悬停解释（不注入卡面）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/archmage_antonidas.png";
    protected override Type MinionType => typeof(AntonidasMinion);

    protected override int MinionAttack => 5;

    protected override int MinionHealth => 7;

    public AntonidasCard()
        : base(2, CardRarity.Rare)
    {
    }
}
