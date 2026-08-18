using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 水元素 (Water Elemental) - 吉安娜衍生随从卡（Token）。召唤 3/6 的 WaterElementalMinion。
/// 冻结：任何受到本随从伤害的角色获得 1 层冻结。
/// 衍生卡：不进入吉安娜卡池，不出现在卡牌奖励与图鉴中（由冰霜女巫吉安娜/深度冻结/冰冷触摸召唤）。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class WaterElementalCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 元素种族 + 冻结（悬停解释）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Elemental, jaina.Scripts.Character.Keywords.JainaKeywords.Freeze,
         CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/water_elemental.png";
    protected override Type MinionType => typeof(WaterElementalMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 6;

    public WaterElementalCard()
        : base(1, CardRarity.Common)
    {
    }
}
