using System;
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
/// 倒带 (Rewind) - 0费：发现一张你在本局对战中施放过的其他攻击牌或技能牌的一张复制。
/// 通过 JainaCastTracker 追踪本局施放过的攻击/技能牌类型。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class Rewind : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/rewind.png";

    public Rewind()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（排除自身后取历史）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        var playedTypes = rec.PlayedAttackSkills
            .Where(t => t != typeof(Rewind) && t != typeof(Fireblast)) // 排除自身与英雄技能（火焰冲击）
            .ToList();
        if (playedTypes.Count == 0)
        {
            return;
        }

        // 从施放过的类型中随机取候选（最多 3 张，不重复）
        var rng = base.Owner.RunState.Rng.CombatTargets;
        var pool = new List<Type>(playedTypes);
        var candidates = new List<CardModel>();
        while (candidates.Count < 3 && pool.Count > 0)
        {
            var type = rng.NextItem(pool);
            if (type == null)
            {
                break;
            }
            pool.Remove(type);
            var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(type));
            if (canonical != null)
            {
                candidates.Add(combatState.CreateCard(canonical, base.Owner));
            }
        }
        if (candidates.Count == 0)
        {
            return;
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, candidates.AsReadOnly(), base.Owner, canSkip: true);
        if (chosen != null)
        {
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(chosen);
            if (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(base.Owner))
            {
                await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, base.Owner);
            }
        }
    }
}
