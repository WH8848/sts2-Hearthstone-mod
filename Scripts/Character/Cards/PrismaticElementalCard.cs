using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 棱光元素 (Prismatic Elemental) - 0费随从卡（罕见，元素种族）。
/// 战吼：发现一张任意角色（全职业）的卡牌，使其费用减少1点。属性 1/2。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class PrismaticElementalCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"棱光元素"（Prismatic Elemental）官方原画（当前为程序绘制占位图，
    /// 网络恢复后可从 wiki.gg 下载替换）
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/prismatic_elemental.png";

    /// <summary>
    /// 自身特性悬停：发现解释（"随从"关键词解释由模板兜底）
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips =>
        [HoverTipFactory.FromKeyword(JainaKeywords.Discover)];

    protected override Type MinionType => typeof(PrismaticElementalMinion);

    protected override int MinionAttack => 1;

    protected override int MinionHealth => 2;

    /// <summary>
    /// 元素种族 + 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Elemental, JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public PrismaticElementalCard()
        : base(0, CardRarity.Uncommon)
    {
    }
}
