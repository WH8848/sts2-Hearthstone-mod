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
/// 诺干农的智慧 (Norgannon's Wisdom) - 1费：抽两张牌。
/// 简化实现（原效果"释放火/奥/冰三系后费用降为0"需追踪派系，暂略）。
/// 升级后变为清凉的泉水 (Cooling Spring)：抽 2 张牌，每抽到一张攻击牌或技能牌回一费。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class NorgannonWisdom : ModCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/refreshing_spring_water.png" : "res://assets/card_art/norgannon_wisdom.png";

    public NorgannonWisdom()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
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

    /// <summary>
    /// 在本局对战中释放过火焰/奥术/冰霜三派系攻击或技能牌各一张后，本牌费用降为 0（实时生效）
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(MegaCrit.Sts2.Core.Models.CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (ReferenceEquals(card, this) && base.CombatState != null &&
            jaina.Scripts.Character.JainaCastTracker.HasAllThreeSchools(base.CombatState))
        {
            modifiedCost = 0m;
            return true;
        }
        return false;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var drawn = await CardPileCmd.Draw(choiceContext, 2, base.Owner);

        // 清凉的泉水（升级后）：每抽到一张攻击牌或技能牌回一费
        if (IsUpgraded)
        {
            foreach (var card in drawn)
            {
                if (card.Type == CardType.Attack || card.Type == CardType.Skill)
                {
                    await PlayerCmd.GainEnergy(1m, base.Owner);
                }
            }
        }
    }
}
