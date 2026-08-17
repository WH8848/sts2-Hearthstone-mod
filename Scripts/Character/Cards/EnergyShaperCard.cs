using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 能量塑形师 (Energy Shaper) - 1费随从卡（罕见）。
/// 战吼：将你手牌中的所有法术牌变形成为费用增加1点的全角色卡牌。（保留其原始费用。）
/// 属性 3/4。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class EnergyShaperCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"能量塑形师"（Energy Shaper, RLK_545）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/energy_shaper.png";

    protected override Type MinionType => typeof(EnergyShaperMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 4;

    /// <summary>
    /// 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public EnergyShaperCard()
        : base(1, CardRarity.Uncommon)
    {
    }
}
