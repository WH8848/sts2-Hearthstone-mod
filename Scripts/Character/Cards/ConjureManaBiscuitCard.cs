using System.Collections.Generic;
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
/// 制造法力饼干 (Conjure Mana Biscuit) - 0费技能牌（罕见，奥术派系）。
/// 将一张可以复原两费的法力饼干置入你的手牌。消耗。
/// 升级后：将一张可以复原两费的法力饼干置入你的手牌（不再消耗）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ConjureManaBiscuitCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 奥术派系；基础版消耗（升级后不再消耗）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane,
           CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：炉石传说"制造法力饼干"（Conjure Mana Biscuit, YOP_019）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/conjure_mana_biscuit.png";

    public ConjureManaBiscuitCard()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
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

        // 将一张法力饼干置入手牌（手牌满不入手）
        if (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(base.Owner))
        {
            var combatState = base.Owner.Creature.CombatState;
            var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(typeof(ManaBiscuitCard)));
            if (canonical != null && combatState != null)
            {
                var biscuit = combatState.CreateCard(canonical, base.Owner);
                jaina.Scripts.Character.JainaCastTracker.MarkGenerated(biscuit);
                await CardPileCmd.AddGeneratedCardToCombat(biscuit, PileType.Hand, base.Owner);
            }
        }
    }
}
