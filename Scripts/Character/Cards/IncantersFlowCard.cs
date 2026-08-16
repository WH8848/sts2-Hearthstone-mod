using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 咒术洪流 (Incanter's Flow) - 2费技能牌（稀有，奥术派系）。
/// 使你抽牌堆中所有法术牌的费用减少1点。消耗。
/// 升级后变为露娜的口袋银河 (Luna's Pocket Galaxy)：使你抽牌堆中所有随从牌的费用变为0点。消耗。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class IncantersFlowCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 奥术派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane,
         CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/lunas_pocket_galaxy.png" : "res://assets/card_art/incanters_flow.png";

    public IncantersFlowCard()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"露娜的口袋银河"
    /// </summary>
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

        var drawPile = base.Owner.PlayerCombatState?.DrawPile;
        if (drawPile == null)
        {
            return;
        }
        if (IsUpgraded)
        {
            // 露娜的口袋银河：抽牌堆中所有随从牌费用变为 0
            foreach (var card in drawPile.Cards.Where(c => c != null && c.Type == JainaCardTypes.Minion).ToList())
            {
                card.EnergyCost.SetCustomBaseCost(0);
            }
        }
        else
        {
            // 咒术洪流：抽牌堆中所有法术牌费用减少 1
            foreach (var card in drawPile.Cards
                         .Where(c => c != null && (c.Type == CardType.Attack || c.Type == CardType.Skill))
                         .ToList())
            {
                card.EnergyCost.SetCustomBaseCost(System.Math.Max(0, card.EnergyCost.GetResolved() - 1));
            }
        }
    }
}
