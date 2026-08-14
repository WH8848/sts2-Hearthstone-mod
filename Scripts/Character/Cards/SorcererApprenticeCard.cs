using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// SorcererApprenticeCard - 吉安娜随从卡（罕见）。1 费，召唤 3/2 的 SorcererApprenticeMinion。
/// 效果：你的法术牌费用减少 1 点（由随从光环 SorcererApprenticePower 实现）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class SorcererApprenticeCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 描述中提及"法术牌"，挂法术牌关键词以提供右侧悬停解释（不注入卡面）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/sorcerer_apprentice.png";
    protected override Type MinionType => typeof(SorcererApprenticeMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 2;

    public SorcererApprenticeCard()
        : base(1, CardRarity.Uncommon)
    {
    }
}
