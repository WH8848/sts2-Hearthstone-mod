using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;
using jaina.Scripts.Character.Keywords;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// KalecgosCard - 吉安娜随从卡。召唤 4/12 的 KalecgosMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class KalecgosCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 龙种族 + 战吼：发现一张法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Dragon, jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry,
         jaina.Scripts.Character.Keywords.JainaKeywords.Spell, CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/kalecgos.png";

    /// <summary>
    /// 自身特性悬停：发现解释（"随从"关键词解释由模板兜底）
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips =>
        [HoverTipFactory.FromKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Discover)];

    protected override Type MinionType => typeof(KalecgosMinion);

    protected override int MinionAttack => 4;

    protected override int MinionHealth => 12;

    public KalecgosCard()
        : base(3, CardRarity.Rare)
    {
    }
}
