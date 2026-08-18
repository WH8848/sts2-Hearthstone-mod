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
/// 将你抽牌堆和弃牌堆中的法术牌变形成为费用消耗增加1点的全角色卡牌。（保留其原始费用消耗。）
/// 基础版消耗；升级后不再消耗。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class DeckOfWondersCard : JainaSpellCardTemplate
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

        // 快照抽牌堆 + 弃牌堆中的法术牌（攻击/技能牌，不含英雄技能卡）
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
                (c.Type == CardType.Attack || c.Type == CardType.Skill) &&
                !HeroPowerHandHelper.IsHeroPowerCard(c) &&
                c.IsTransformable));
        }

        foreach (var spell in spells)
        {
            if (spell == null || spell.Pile == null)
            {
                continue;
            }
            // 原牌原始费用
            int originalCost = spell.EnergyCost.Canonical;
            // 目标池：所有法术牌（攻击/技能牌）中原始费用 = 原费用 + 1 的牌，
            // 每种按可升级级别展开（未升级形态与升级形态（+）都可作为变形目标）；
            // 应用 Jaina 随机池统一排除（7 个非角色池/先古稀有度/多人专属）
            var candidateTypes = ModelDb.AllCards
                .Where(c => c != null &&
                            (c.Type == CardType.Attack || c.Type == CardType.Skill) &&
                            c.EnergyCost.Canonical == originalCost + 1 &&
                            !HeroPowerHandHelper.IsHeroPowerCard(c) &&
                            jaina.Scripts.Character.JainaRandomPoolHelper.IsEligible(c))
                .ToList();
            if (candidateTypes.Count == 0)
            {
                continue;
            }
            var chosenType = rng.NextItem(candidateTypes);
            if (chosenType == null)
            {
                continue;
            }
            int maxLevel = Math.Min(chosenType.MaxUpgradeLevel, 2);
            int upgradeLevel = rng.NextInt(0, maxLevel + 1);

            // 生成带 Owner 的变形目标实例（Transform 要求 replacement.Owner == original.Owner）
            var replacement = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, base.Owner, chosenType.GetType(), upgradeLevel);
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
