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
/// 倒带 (Rewind) - 0费：发现一张你在本局对战中施放过的攻击牌或技能牌的一张复制。
/// 简化实现：从吉安娜攻击/技能牌池中发现一张复制。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class Rewind : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/rewind.png";

    public Rewind()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await JainaDiscoverHelper.DiscoverAndAddToHand(choiceContext, base.Owner);
    }
}
