using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 麦迪文的残影 (Medivh's Echo) - 1费技能（罕见，奥术派系）。
/// 将每个友方随从的各一张复制置入你的手牌。
/// 升级后变为"幻觉药水 (Illusion Potion)"：将你的所有随从的 1/1 复制置入你的手牌，
/// 并使其法力值消耗变为 0 点。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MedivhEcho : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌；奥术派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：麦迪文的残影 / 升级后（幻觉药水）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/illusion_potion.png" : "res://assets/card_art/medivh_echo.png";

    public MedivhEcho()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"幻觉药水 (Illusion Potion)"
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

        var owner = base.Owner;
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 收集友方存活随从（玩家侧宠物中的 Jaina 随从）
        var minions = owner.PlayerCombatState?.Pets
            .Where(p => p != null && p.IsAlive && p.Monster is JainaMinionBase)
            .ToList() ?? [];

        foreach (var minion in minions)
        {
            // 手牌满时不入手（0.111.1 满手时 Add 会把牌静默改道弃牌堆）
            if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(owner))
            {
                break;
            }

            var cardType = JainaMinionCardMap.GetCardType(minion.Monster.GetType());
            if (cardType == null)
            {
                continue;
            }
            var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, owner, cardType, 0);
            if (card == null)
            {
                continue;
            }

            if (IsUpgraded && card is JainaMinionCardTemplate minionCard)
            {
                // 幻觉药水：1/1 复制，法力值消耗变为 0 点（直到打出）
                minionCard.SetOverrideStats(1, 1);
                card.EnergyCost.SetUntilPlayed(0);
            }

            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, owner);
        }
    }
}
