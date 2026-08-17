using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 滑冰元素 (Sleet Skater) - 2费随从卡（罕见）。
/// 微缩，战吼：给于敌方1层冻结，获得等同于其减少的总体伤害的格挡。属性 3/4。
/// 微型复制品（0费1/1）由微缩系统自动生成，保留全部文字效果。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class SkatingElementalCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"滑冰元素"（Sleet Skater, WBW_103348）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/sleet_skater.png";

    /// <summary>
    /// 微缩（打出后生成0费1/1微型复制品）+ 战吼（悬停解释）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Miniaturize, JainaKeywords.Battlecry];

    protected override Type MinionType => typeof(SkatingElementalMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 4;

    public SkatingElementalCard()
        : base(2, CardRarity.Uncommon)
    {
    }
}
