using System;
using MegaCrit.Sts2.Core.Entities.Cards;
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

    public AntonidasCard()
        : base(2, CardRarity.Rare)
    {
    }
}
