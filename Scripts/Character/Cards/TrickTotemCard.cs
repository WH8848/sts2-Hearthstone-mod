using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 戏法图腾 (Trick Totem) - 1费随从卡（普通）。属性 0/3。
/// 在你的回合结束时，随机施放一个费用消耗小于或等于1点的全角色卡牌。
/// 升级不减费（模板默认：去除"消耗"关键词）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class TrickTotemCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"戏法图腾"官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/trick_totem.png";

    protected override Type MinionType => typeof(TrickTotemMinion);

    protected override int MinionAttack => 0;

    protected override int MinionHealth => 3;

    /// <summary>
    /// 消耗（随从卡打出后消耗，模板默认；升级后自动去除）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public TrickTotemCard()
        : base(1, CardRarity.Common)
    {
    }
}
