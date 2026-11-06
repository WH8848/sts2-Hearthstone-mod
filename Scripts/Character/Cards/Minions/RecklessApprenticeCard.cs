using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 鲁莽的学徒 (Reckless Apprentice) - 1费随从卡（罕见）。
/// 战吼：向随机敌人发射8次你的英雄技能。属性 3/5。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class RecklessApprenticeCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"鲁莽的学徒"（Reckless Apprentice, BAR_544）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/reckless_apprentice.png";

    protected override Type MinionType => typeof(RecklessApprenticeMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 5;

    /// <summary>
    /// 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public RecklessApprenticeCard()
        : base(1, CardRarity.Uncommon)
    {
    }
}
