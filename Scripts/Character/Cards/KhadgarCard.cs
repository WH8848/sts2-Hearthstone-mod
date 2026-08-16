using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using jaina.Scripts.Character.Weapons;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 益智大师卡德加 (Khadgar) - 2费随从卡（稀有）。
/// 战吼：装备一个会施放有用的法师法术的 0/6 的魔法智慧之球！
/// 属性 5/5。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class KhadgarCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"益智大师卡德加"（Puzzlemaster Khadgar, TOY_373）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/puzzlemaster_khadgar.png";

    protected override Type MinionType => typeof(KhadgarMinion);

    protected override int MinionAttack => 5;

    protected override int MinionHealth => 5;

    /// <summary>
    /// 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public KhadgarCard()
        : base(2, CardRarity.Rare)
    {
    }

    /// <summary>
    /// 悬停提示：显示战吼装备的衍生物"魔法智慧之球"卡（参考灵体采集者显示小精灵）
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return new CardHoverTip(ModelDb.Card<WondrousWisdomballCard>());
        }
    }
}
