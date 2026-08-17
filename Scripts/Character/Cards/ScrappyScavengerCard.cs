using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 拾荒清道夫 (Scrappy Scavenger) - 0费随从卡（罕见）。
/// 战吼：发现一张费用消耗等同于你剩余费用的卡牌。属性 1/1。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ScrappyScavengerCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"拾荒清道夫"（Scrappy Scavenger, 118192）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/scrappy_scavenger.png";

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromKeyword(DescriptionKeywords.Minion),
         HoverTipFactory.FromKeyword(JainaKeywords.Discover)];

    protected override Type MinionType => typeof(ScrappyScavengerMinion);

    protected override int MinionAttack => 1;

    protected override int MinionHealth => 1;

    /// <summary>
    /// 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public ScrappyScavengerCard()
        : base(0, CardRarity.Uncommon)
    {
    }
}
