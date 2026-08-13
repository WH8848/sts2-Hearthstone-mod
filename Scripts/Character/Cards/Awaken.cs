using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 唤醒 (Awaken) - 0费：用随机法师攻击牌或技能牌填满你的手牌。这些牌具有虚无。
/// 升级后变为远古雕文 (Ancient Glyph)：发现一张攻击牌或技能牌，使其费用减少 1 点。
/// 再升级变为巅峰无限 (Peak Infinity)：发现一张法术牌，使其费用减少 1 点。压轴：回合结束时将本牌移回手牌。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class Awaken : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/awaken.png";

    public Awaken()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.None, true)
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
            // 远古雕文 → 巅峰无限（二级升级名用不同的 key）
            string suffix = CurrentUpgradeLevel >= 2 ? ".titleUpgraded2" : ".titleUpgraded";
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + suffix);
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/诺干农派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        if (IsUpgraded)
        {
            // 远古雕文/巅峰无限：发现一张牌，费用减少 1 点
            var chosen = await JainaDiscoverHelper.DiscoverAndAddToHand(choiceContext, base.Owner, maxCost: 9);
            if (chosen != null && chosen.EnergyCost.Canonical > 0)
            {
                // 简化：直接减少 1 点展示费用（mutable 实例）
                chosen.EnergyCost.SetUntilPlayed((int)chosen.EnergyCost.Canonical - 1);
            }

            // 巅峰无限（二级升级）压轴：刚好消耗完能量时，回合结束将本牌移回手牌
            if (CurrentUpgradeLevel >= 2 && base.Owner.PlayerCombatState is { Energy: <= 0 })
            {
                var power = await PowerCmd.Apply<jaina.Scripts.Character.Powers.PeakInfinityPower>(
                    choiceContext, base.Owner.Creature, 1m, base.Owner.Creature, this);
                if (power != null)
                {
                    power.TargetCard = this;
                }
            }
        }
        else
        {
            // 唤醒：用随机法师攻击/技能牌填满手牌（虚无）
            var hand = base.Owner.PlayerCombatState?.Hand;
            if (hand != null)
            {
                int toAdd = Math.Max(0, STS2RitsuLib.RitsuLibFramework.GetMaxHandSize(base.Owner) - hand.Cards.Count);
                for (int i = 0; i < toAdd; i++)
                {
                    var card = JainaDiscoverHelper.RollCandidates(base.Owner, count: 1).FirstOrDefault();
                    if (card != null)
                    {
                        card.AddKeyword(CardKeyword.Ethereal);
                        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
                        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, base.Owner);
                    }
                }
            }
        }
    }
}
