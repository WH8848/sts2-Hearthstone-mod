using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 冬泉雏龙 (Winterspring Whelp) - 0费随从卡（罕见，龙种族）。
/// 战吼：发现一张任意角色的费用为0的卡牌。属性 1/2。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class WinterspringWhelpCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"冬泉雏龙"（Winterspring Whelp, CATA_484）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/winterspring_whelp.png";

    /// <summary>
    /// 自身特性悬停：发现解释（"随从"关键词解释由模板兜底）
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips =>
        [HoverTipFactory.FromKeyword(JainaKeywords.Discover)];

    protected override Type MinionType => typeof(WinterspringWhelpMinion);

    protected override int MinionAttack => 1;

    protected override int MinionHealth => 2;

    /// <summary>
    /// 龙种族 + 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Dragon, JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public WinterspringWhelpCard()
        : base(0, CardRarity.Uncommon)
    {
    }
}
