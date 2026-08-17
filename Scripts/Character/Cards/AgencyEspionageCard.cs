using System.Collections.Generic;
using System.Linq;
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
/// 旅社谍战 (Agency Espionage) - 1费技能牌（罕见）。
/// 将每个其他角色的各一张牌洗入你的抽牌堆，其法力值消耗为0点。抽取其中一张。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class AgencyEspionageCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌（无派系）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/agency_espionage.png";

    public AgencyEspionageCard()
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
        var rng = owner.RunState.Rng.CombatTargets;

        // 每个其他角色（角色卡池）各取一张牌洗入抽牌堆（随机位置），费用变为 0
        var jainaPool = ModelDb.CardPool<jaina.Scripts.Character.JainaCardPool>();
        foreach (var pool in ModelDb.AllCharacterCardPools)
        {
            if (pool == jainaPool)
            {
                continue; // 排除自己（吉安娜）
            }
            var candidates = pool.AllCards.Where(c => c != null).ToList();
            if (candidates.Count == 0)
            {
                continue;
            }
            var chosen = rng.NextItem(candidates);
            if (chosen == null)
            {
                continue;
            }
            var copy = combatState.CreateCard(chosen, owner);
            if (copy == null)
            {
                continue;
            }
            copy.EnergyCost.SetCustomBaseCost(0);
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(copy);
            await CardPileCmd.Add(copy, PileType.Draw, CardPilePosition.Random);
        }

        // 抽取其中一张
        await CardPileCmd.Draw(choiceContext, 1, owner);
    }
}
