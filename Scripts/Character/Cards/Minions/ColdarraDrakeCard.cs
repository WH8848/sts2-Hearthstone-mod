using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 考达拉幼龙 (Coldarra Drake) - 2费随从卡（罕见）。
/// 你的英雄技能变为1费，你可以使用任意次数的英雄技能。属性 6/7。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ColdarraDrakeCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"考达拉幼龙"（Coldarra Drake, TGT_025）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/coldarra_drake.png";

    protected override Type MinionType => typeof(ColdarraDrakeMinion);

    protected override int MinionAttack => 6;

    protected override int MinionHealth => 7;

    /// <summary>
    /// 龙种族 + 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Dragon, CardKeyword.Exhaust];

    public ColdarraDrakeCard()
        : base(2, CardRarity.Uncommon)
    {
    }
}
