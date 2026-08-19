using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// ArcaneArtificerCard - 吉安娜随从卡。召唤 1/3 的 ArcaneArtificerMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ArcaneArtificerCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 元素种族 + 消耗（随从卡模板默认）。
    /// 注意：不挂"法术牌"关键词——Spell 是"法术牌"内部判定标记（isSpellCard），
    /// 随从卡挂上会被误判为法术牌混入发现池/被倒带/任务进度等误认（历史遗留教训）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Elemental, CardKeyword.Exhaust];

    /// <summary>
    /// 自身特性悬停：格挡关键词注释（描述中"获得格挡"）（"随从"关键词解释由模板兜底）
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips
    {
        get
        {
            yield return HoverTipFactory.Static(StaticHoverTip.Block);
        }
    }

    public override string CustomPortraitPath => "res://assets/card_art/arcane_artificer.png";
    protected override Type MinionType => typeof(ArcaneArtificerMinion);

    protected override int MinionAttack => 1;

    protected override int MinionHealth => 3;

    public ArcaneArtificerCard()
        : base(0, CardRarity.Common)
    {
    }
}
