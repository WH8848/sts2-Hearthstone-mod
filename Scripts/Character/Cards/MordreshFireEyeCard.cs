using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 火眼莫德雷斯 (Mordresh Fire Eye) - 3费随从卡（稀有）。
/// 战吼：在本局对战中，如果你用你的英雄技能累计造成了10点伤害，
/// 则对所有敌人造成4次10点伤害。属性 8/8。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MordreshFireEyeCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"火眼莫德雷斯"（Mordresh Fire Eye, BAR_547）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/mordresh_fire_eye.png";

    protected override Type MinionType => typeof(MordreshFireEyeMinion);

    protected override int MinionAttack => 8;

    protected override int MinionHealth => 8;

    /// <summary>
    /// 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public MordreshFireEyeCard()
        : base(3, CardRarity.Rare)
    {
    }
}
