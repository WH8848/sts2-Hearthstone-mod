using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// SorcererApprenticeCard - 吉安娜随从卡（罕见）。1 费，召唤 3/2 的 SorcererApprenticeMinion。
/// 效果：当 4 只巫师学徒在场时，你的法术牌费用减少 1 点（由随从光环 SorcererApprenticePower 实现）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class SorcererApprenticeCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 关键词：消耗（随从卡模板默认）。
    /// 注意：不挂"法术牌"关键词——Spell 是"法术牌"内部判定标记（isSpellCard），
    /// 随从卡挂上会被误判为法术牌混入发现池/被倒带/任务进度等误认（历史遗留教训）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/sorcerer_apprentice.png";
    protected override Type MinionType => typeof(SorcererApprenticeMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 2;

    public SorcererApprenticeCard()
        : base(1, CardRarity.Common)
    {
    }
}
