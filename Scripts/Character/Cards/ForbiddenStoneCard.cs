using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 源生之石 (Forbidden Stone) - 1费能力牌（衍生卡，稀有）。
/// 在你发现一张牌后，会自动使用其余选项并失去1点耐久度。耐久度8。耐久度0时能力消失。
/// 武器能力卡：不升级也不可升级。
/// 由禁忌序列作为奖励直接置入手牌。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class ForbiddenStoneCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 武器能力卡不可升级（IsUpgradable=false，不出现在升级候选）
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, jaina.Scripts.Character.Keywords.JainaKeywords.Weapon,
         jaina.Scripts.Character.Keywords.JainaKeywords.Durability];

    public override string CustomPortraitPath => "res://assets/card_art/forbidden_stone.png";

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Discover)];

    public ForbiddenStoneCard()
        : base(1, CardType.Power, CardRarity.Token, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称不变
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            return title.GetFormattedText();
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂光环：每发现一张牌后自动使用其余选项并失去 1 点耐久度（耐久 8，0 时消失）
        await PowerCmd.Apply<ForbiddenStonePower>(
            choiceContext, [base.Owner.Creature], 8m, base.Owner.Creature, this);
    }
}
