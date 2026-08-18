using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 天定之灾克尔苏加德 (Kel'Thuzad, the Inevitable) - 3费随从卡（稀有，亡灵种族）。
/// 战吼：复活你的不稳定的骷髅。战场上放不下的骷髅会立即爆炸。属性 6/8。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class KelThuzadCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"天定之灾克尔苏加德"（Kel'Thuzad, the Inevitable, REV_514）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/kelthuzad_inevitable.png";

    /// <summary>
    /// 自身特性悬停：战吼复活的"不稳定的骷髅"衍生物卡（"随从"关键词解释由模板兜底）
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips
    {
        get
        {
            yield return new CardHoverTip(MegaCrit.Sts2.Core.Models.ModelDb.Card<VolatileSkeletonCard>());
        }
    }

    protected override Type MinionType => typeof(KelThuzadMinion);

    protected override int MinionAttack => 6;

    protected override int MinionHealth => 8;

    /// <summary>
    /// 亡灵种族 + 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Undead, JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public KelThuzadCard()
        : base(3, CardRarity.Rare)
    {
    }
}
