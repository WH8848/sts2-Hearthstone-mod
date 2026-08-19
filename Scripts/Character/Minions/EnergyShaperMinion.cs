using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 能量塑形师 (Energy Shaper) - 吉安娜专属随从。
/// 属性：攻击 3，生命 4。
/// 战吼：将你手牌中的所有法术牌变形成为费用增加1点的全角色卡牌。（保留其原始费用。）
/// 仅手牌打出时触发。
/// </summary>
[RegisterMonster]
public sealed class EnergyShaperMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    /// <summary>
    /// 战斗视觉：能量塑形师卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/energy_shaper.png";

    /// <summary>
    /// 战吼：将手牌中所有法术牌（攻击/技能牌，或带"法术牌"关键词的能力牌，
    /// 不含英雄技能卡与任务卡）变形为一张随机全角色攻击/技能/能力牌
    /// （Attack/Skill/Power——含吉安娜法术牌，吉安娜的非法术能力牌
    /// 如戏法图腾/炉石形态不在范围内），其原始费用 = 原牌费用 + 1；
    /// 变形后的牌保留原牌费用显示。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        var hand = PileType.Hand.GetPile(owner);
        if (hand == null)
        {
            return;
        }

        // 快照手牌中的法术牌（统一判定：攻击/技能，或带"法术牌"关键词的能力牌；
        // 排除英雄技能卡与任务卡——任务卡不可被变形破坏任务线）
        var spells = hand.Cards
            .Where(c => c != null &&
                        jaina.Scripts.Character.JainaCastTracker.IsSpellCard(c) &&
                        !HeroPowerHandHelper.IsHeroPowerCard(c) &&
                        !c.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Quest))
            .ToList();
        if (spells.Count == 0)
        {
            return;
        }

        var rng = owner.RunState.Rng.CombatCardSelection;
        var combatState = Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        foreach (var spell in spells)
        {
            if (spell == null || spell.Pile == null || !spell.IsTransformable)
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
            var candidates = new List<CardModel>();
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
                        combatState, owner, canonical.GetType(), level);
                    if (card != null)
                    {
                        candidates.Add(card);
                    }
                }
            }
            if (candidates.Count == 0)
            {
                continue;
            }
            // 随机选一个形态实例（含升级形态）作为变形目标
            var replacement = rng.NextItem(candidates);
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
