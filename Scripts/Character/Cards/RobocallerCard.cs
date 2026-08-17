using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 拨号机器人 (Robocaller) - 1费随从卡（普通）。
/// 战吼：抽取费用消耗为2，2和2的牌各一张。（每回合随机拨号！）属性 3/2。
/// 实际拨号：战吼时随机三个 0~4 的数字（可重复），从抽牌堆定向抽取费用匹配的牌各一张。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class RobocallerCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"拨号机器人"（Robocaller, 110757）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/robocaller.png";

    protected override Type MinionType => typeof(RobocallerMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 2;

    /// <summary>
    /// 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public RobocallerCard()
        : base(1, CardRarity.Common)
    {
    }
}
