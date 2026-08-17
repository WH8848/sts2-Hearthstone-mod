using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 云雾王子 (Cloud Prince) - 2费随从卡（普通，元素）。
/// 战吼：你的状态栏中每有1种状态，则造成6点伤害。属性 4/4。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class CloudPrinceCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"云雾王子"（Cloud Prince, SoU_54493）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/cloud_prince.png";

    /// <summary>
    /// 元素种族 + 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Elemental, JainaKeywords.Battlecry, CardKeyword.Exhaust];

    protected override Type MinionType => typeof(CloudPrinceMinion);

    protected override int MinionAttack => 4;

    protected override int MinionHealth => 4;

    public CloudPrinceCard()
        : base(2, CardRarity.Common)
    {
    }
}
