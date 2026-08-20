using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 霜冻元素 (Frost Elemental) - 吉安娜衍生随从卡（Token）。召唤 1/1 的 FrostElementalMinion。
/// 冻结：任何受到本随从伤害的角色获得 1 层冻结。
/// 衍生卡：不进入吉安娜卡池，不出现在卡牌奖励与图鉴中。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class FrostElementalCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 元素种族 + 冻结（悬停解释）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Elemental, jaina.Scripts.Character.Keywords.JainaKeywords.Freeze,
         CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/frost_elemental.png";
    protected override Type MinionType => typeof(FrostElementalMinion);

    protected override int MinionAttack => 1;

    protected override int MinionHealth => 1;

    public FrostElementalCard()
        : base(0, CardRarity.Token)
    {
    }
}
