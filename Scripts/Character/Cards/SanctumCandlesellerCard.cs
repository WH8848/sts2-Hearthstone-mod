using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 圣殿蜡烛商 (Sanctum Candleseller) - 吉安娜随从卡。召唤 4/5 的 SanctumCandlesellerMinion。
/// 在你施放一个火焰法术后，抽一张法术牌。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class SanctumCandlesellerCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 元素种族（炉石 Sanctum Chandler 为元素）；无其他特殊词条
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Elemental, CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/sanctum_chandler.png";
    protected override Type MinionType => typeof(SanctumCandlesellerMinion);

    protected override int MinionAttack => 4;

    protected override int MinionHealth => 5;

    public SanctumCandlesellerCard()
        : base(2, CardRarity.Uncommon)
    {
    }
}
