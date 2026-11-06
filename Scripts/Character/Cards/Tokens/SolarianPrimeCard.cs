using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 终极索兰莉安 (Solarian Prime) - 2费随从卡（衍生）。
/// 力量+1。战吼：随机施放五个法师法术（尽可能以敌人为目标）。属性 7/7。
/// 由星术师索兰莉安的亡语洗入牌库，不进入掉落池。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class SolarianPrimeCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"终极索兰莉安"（Solarian Prime, BT_028t）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/solarian_prime.png";

    protected override Type MinionType => typeof(SolarianPrimeMinion);

    protected override int MinionAttack => 7;

    protected override int MinionHealth => 7;

    /// <summary>
    /// 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public SolarianPrimeCard()
        : base(2, CardRarity.Token)
    {
    }
}
