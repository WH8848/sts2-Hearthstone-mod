using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 魔术戏法 (Trick) - 0费：发现一张费用小于或等于 1 点的攻击牌或技能牌。
/// 升级后变为广阔智慧 (Broad Wisdom)：发现两张费用小于或等于 1 点的攻击牌或技能牌，交换其费用消耗。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class Trick : ModCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/trick.png";

    public Trick()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.None, true)
    {
    }

    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        if (IsUpgraded)
        {
            // 广阔智慧：发现两张费用≤1的攻击/技能牌，交换其费用消耗
            var first = await JainaDiscoverHelper.SelectCandidate(choiceContext, base.Owner, maxCost: 1);
            var second = await JainaDiscoverHelper.SelectCandidate(choiceContext, base.Owner, maxCost: 1);
            if (first != null && second != null)
            {
                // 交换展示费用（SetUntilPlayed 直到打出）
                int cost1 = first.EnergyCost.GetResolved();
                int cost2 = second.EnergyCost.GetResolved();
                first.EnergyCost.SetUntilPlayed(cost2);
                second.EnergyCost.SetUntilPlayed(cost1);
            }
            if (first != null)
            {
                jaina.Scripts.Character.JainaCastTracker.MarkGenerated(first);
                await CardPileCmd.AddGeneratedCardToCombat(first, PileType.Hand, base.Owner);
            }
            if (second != null)
            {
                jaina.Scripts.Character.JainaCastTracker.MarkGenerated(second);
                await CardPileCmd.AddGeneratedCardToCombat(second, PileType.Hand, base.Owner);
            }
            return;
        }

        // 魔术戏法：发现一张费用≤1的攻击/技能牌
        var chosen = await JainaDiscoverHelper.DiscoverAndAddToHand(choiceContext, base.Owner, maxCost: 1);
        if (chosen != null)
        {
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(chosen);
        }
    }
}
