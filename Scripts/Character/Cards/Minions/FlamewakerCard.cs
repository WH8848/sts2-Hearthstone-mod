using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 火妖 (Flamewaker) - 吉安娜随从卡。召唤 2/4 的 FlamewakerMinion。
/// 在你施放一个法术后，造成 2 次 1 点伤害，随机分配到所有敌人身上。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class FlamewakerCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 无特殊关键词（效果为施放法术触发，不需词条）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/flamewaker.png";
    protected override Type MinionType => typeof(FlamewakerMinion);

    protected override int MinionAttack => 2;

    protected override int MinionHealth => 4;

    public FlamewakerCard()
        : base(1, CardRarity.Uncommon)
    {
    }
}
