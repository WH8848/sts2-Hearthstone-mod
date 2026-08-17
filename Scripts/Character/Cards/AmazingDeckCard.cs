using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 惊奇套牌 (Deck of Wonders) - 1费技能牌（罕见，奥术派系）。
/// 将五张惊奇卡牌洗入你的抽牌堆。抽到时随机施放一个全角色卡牌。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class AmazingDeckCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 奥术派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Spell, JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/deck_of_wonders.png";

    public AmazingDeckCard()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var owner = base.Owner;
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(typeof(AmazingCard)));
        if (canonical == null)
        {
            return;
        }
        // 将五张惊奇卡牌洗入抽牌堆（随机位置）
        for (int i = 0; i < 5; i++)
        {
            var card = combatState.CreateCard(canonical, owner);
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Random);
        }
    }
}
