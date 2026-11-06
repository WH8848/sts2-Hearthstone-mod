using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// AntonidasCard - 吉安娜随从卡。召唤 5/7 的 AntonidasMinion。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class AntonidasCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 关键词：消耗（随从卡模板默认）。
    /// 注意：不挂"法术牌"关键词——Spell 是"法术牌"内部判定标记（isSpellCard），
    /// 随从卡挂上会被误判为法术牌混入发现池/被倒带/任务进度等误认（历史遗留教训）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/archmage_antonidas.png";
    protected override Type MinionType => typeof(AntonidasMinion);

    protected override int MinionAttack => 5;

    protected override int MinionHealth => 7;

    /// <summary>
    /// 衍生卡悬停：大法师安东尼达斯的效果与"火球术"相关（施放法术后获得火球术），
    /// 卡面悬停显示火球术衍生卡（同深度冻结/绿洲盟军做法）。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips =>
    [
        new CardHoverTip(ModelDb.Card<Fireball>())
    ];

    public AntonidasCard()
        : base(2, CardRarity.Rare)
    {
    }
}
