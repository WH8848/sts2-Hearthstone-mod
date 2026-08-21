using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 宇宙异象 (Cosmic Anomaly) - 1费随从卡（罕见，元素种族）。
/// 力量+2（在场期间玩家力量 +2，随从死亡时移除）。属性 4/3。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class CosmicAnomalyCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：程序绘制的宇宙异象占位原画（深空星云 + 发光异象之眼）；
    /// 如有官方原画可替换
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/cosmic_anomaly.png";

    protected override Type MinionType => typeof(CosmicAnomalyMinion);

    protected override int MinionAttack => 4;

    protected override int MinionHealth => 3;

    /// <summary>
    /// 元素种族 + 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Elemental, CardKeyword.Exhaust];

    public CosmicAnomalyCard()
        : base(1, CardRarity.Uncommon)
    {
    }
}
