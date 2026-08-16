using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 水元素 (Water Elemental) - 吉安娜随从卡。召唤 3/6 的 WaterElementalMinion。
/// 冻结：任何受到本随从伤害的角色获得 1 层冻结。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class WaterElementalCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 冻结（悬停解释）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Freeze, CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/water_elemental.png";
    protected override Type MinionType => typeof(WaterElementalMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 6;

    public WaterElementalCard()
        : base(1, CardRarity.Common)
    {
    }
}
