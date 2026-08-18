using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 愚人套牌 (Deck of Lunacy) - 0费技能牌（稀有，奥术派系）。
/// 将你抽牌堆和弃牌堆中的法术牌变形成为费用消耗增加1点的全角色攻击/技能/能力牌
/// （Attack/Skill/Power——含吉安娜法术牌，吉安娜的非法术能力牌
/// 如戏法图腾/炉石形态不在范围内）。（保留其原始费用消耗。）
/// 基础版消耗；升级后不再消耗。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class DeckOfWondersCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌（无派系）；基础版消耗（升级后不再消耗）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 可升级（升级后去除消耗）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 卡牌原画：炉石传说"愚人套牌"（Deck of Lunacy, DMF_712）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/deck_of_lunacy.png";

    public DeckOfWondersCard()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级：移除消耗（LocalKeywords 懒初始化只算一次，升级形态的 Keywords
    /// 缓存自基础状态——需显式移除 Exhaust，否则升级后卡面仍显示"消耗"）。
    /// 无派系卡，升级不需要派系处理（基础/升级都不带 Arcane）。
    /// </summary>
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rng = base.Owner.RunState.Rng.CombatCardSelection;

        // 快照抽牌堆 + 弃牌堆中的法术牌（isSpellCard 定义：攻击/技能牌，
        // 或带"法术牌"关键词的能力牌；不含英雄技能卡与任务卡——任务卡不可被变形破坏任务线）
        var spells = new List<CardModel>();
        foreach (var pileType in new[] { PileType.Draw, PileType.Discard })
        {
            var pile = pileType.GetPile(base.Owner);
            if (pile == null)
            {
                continue;
            }
            spells.AddRange(pile.Cards.Where(c =>
                c != null &&
                (c.Type == CardType.Attack || c.Type == CardType.Skill ||
                 c.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Spell)) &&
                !HeroPowerHandHelper.IsHeroPowerCard(c) &&
                !c.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Quest) &&
                c.IsTransformable));
        }

        foreach (var spell in spells)
        {
            if (spell == null || spell.Pile == null)
            {
                continue;
            }
            // 原牌当前基础费用（含升级减费——变形后保留的是卡面上显示的费用）
            int originalCost = spell.EnergyCost.GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers.None);
            // 目标池：全角色攻击/技能/能力牌（Attack/Skill/Power——含吉安娜法术牌：
            // 攻击/技能牌及带"法术牌"关键词的能力牌；吉安娜的非法术能力牌
            // 如戏法图腾/炉石形态不在范围内，不含英雄技能卡）中
            // 原始费用 = 原费用 + 1 的牌，按可升级级别展开
            // （未升级与升级形态（+）都是独立变形目标）；
            // 应用 Jaina 随机池统一排除（8 个非角色/衍生池/任务卡/先古稀有度/多人专属）
            var candidateCards = new List<CardModel>();
            foreach (var canonical in ModelDb.AllCards)
            {
                if (canonical == null)
                {
                    continue;
                }
                if (canonical.Type != CardType.Attack && canonical.Type != CardType.Skill &&
                    canonical.Type != CardType.Power)
                {
                    continue;
                }
                // 吉安娜非法术能力牌（戏法图腾/炉石形态）不在范围内
                if (jaina.Scripts.Character.JainaCastTracker.IsExcludedFromSpellPool(canonical.GetType()))
                {
                    continue;
                }
                if (canonical.EnergyCost.Canonical != originalCost + 1)
                {
                    continue;
                }
                if (HeroPowerHandHelper.IsHeroPowerCard(canonical))
                {
                    continue;
                }
                if (!jaina.Scripts.Character.JainaRandomPoolHelper.IsEligible(canonical))
                {
                    continue;
                }
                int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(canonical.GetType());
                for (int level = 0; level <= maxLevel; level++)
                {
                    var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                        combatState, base.Owner, canonical.GetType(), level);
                    if (card != null)
                    {
                        candidateCards.Add(card);
                    }
                }
            }
            if (candidateCards.Count == 0)
            {
                continue;
            }
            // 随机选一个形态实例（含升级形态）作为变形目标
            var replacement = rng.NextItem(candidateCards);
            if (replacement == null)
            {
                continue;
            }
            // 保留原始费用：变形后的牌仍显示原牌费用
            replacement.EnergyCost.SetCustomBaseCost(originalCost);

            await CardCmd.Transform(spell, replacement);
        }
    }
}
