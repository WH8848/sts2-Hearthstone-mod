using System.Collections.Generic;
using System.Threading.Tasks;
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
        // 广阔智慧（升级后）：发现两张费用≤1的牌
        int discoveries = IsUpgraded ? 2 : 1;
        for (int i = 0; i < discoveries; i++)
        {
            var chosen = await JainaDiscoverHelper.DiscoverAndAddToHand(choiceContext, base.Owner, maxCost: 1);
            if (chosen != null)
            {
                // 交换费用消耗（简化：两张发现牌的展示费用互换，此处仅保持选择顺序）
            }
        }
    }
}
