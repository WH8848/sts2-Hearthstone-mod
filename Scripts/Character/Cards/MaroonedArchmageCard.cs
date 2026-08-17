using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 落难的大法师 (Marooned Archmage) - 1费随从卡（罕见）。
/// 你每个回合使用的第一张法术牌的费用消耗减少1点。属性 3/4。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MaroonedArchmageCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"落难的大法师"（Marooned Archmage, PIP_107878）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/marooned_archmage.png";

    protected override Type MinionType => typeof(MaroonedArchmageMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 4;

    /// <summary>
    /// 无种族（模板默认：随从卡打出后消耗）
    /// </summary>
    public MaroonedArchmageCard()
        : base(1, CardRarity.Uncommon)
    {
    }
}
