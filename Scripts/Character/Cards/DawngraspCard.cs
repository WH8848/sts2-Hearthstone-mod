using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 奥术师晨拥 (Arcanist Dawngrasp) - 吉安娜随从卡。召唤 8/8 的 DawngraspMinion。
/// 战吼：力量+3。由"抵达传送大厅"任务奖励入手（稀有）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class DawngraspCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 战吼（悬停解释）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/arcanist_dawngrasp.png";
    protected override Type MinionType => typeof(DawngraspMinion);

    protected override int MinionAttack => 8;

    protected override int MinionHealth => 8;

    public DawngraspCard()
        : base(2, CardRarity.Rare)
    {
    }
}
