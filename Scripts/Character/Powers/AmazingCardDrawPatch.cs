using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MinionLib.Targeting;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 惊奇卡牌抽到时：随机施放一个全角色卡牌（随机合法目标，联机可打队友），
/// 释放后此卡消耗。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), "AfterCardDrawn",
    new[] { typeof(ICombatState), typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool) })]
public static class AmazingCardDrawPatch
{
    public static void Postfix(PlayerChoiceContext __1, CardModel __2)
    {
        var card = __2;
        if (card is not Cards.AmazingCard)
        {
            return;
        }
        // 串行执行：回合开始可能同时抽到多张惊奇卡牌，多个并发异步任务
        // 的 await 恢复时序在两端机器上不同 → 随机施放顺序两端相反 →
        // Pile Play 顺序分歧 + PlayerChoiceContext 栈交错 → StateDivergence 断联。
        // 按抽牌顺序排队逐个执行（抽牌顺序两端确定一致）。
        JainaSerialExecutor.Enqueue(__1, card, Trigger);
    }

    /// <summary>
    /// 随机施放一个全角色卡牌（攻击/技能牌，不含英雄技能卡），施放后惊奇卡牌消耗。
    /// </summary>
    private static async Task Trigger(PlayerChoiceContext choiceContext, CardModel amazingCard)
    {
        try
        {
            var player = amazingCard.Owner;
            if (player == null || player.Creature?.CombatState == null)
            {
                return;
            }
            var combatState = player.Creature.CombatState;
            var rng = player.RunState.Rng.CombatTargets;

            // 全角色攻击/技能/能力牌候选（Attack/Skill/Power——含吉安娜法术牌：
            // 攻击/技能牌及带"法术牌"关键词的能力牌；吉安娜的非法术能力牌
            // 如戏法图腾/炉石形态不在范围内；不含英雄技能卡），
            // 按可升级级别展开：每种牌的未升级与升级形态（+）都是独立候选
            // （应用 Jaina 随机池统一排除：
            // 8 个非角色/衍生池/任务卡/先古稀有度/多人专属）
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
                        combatState, player, canonical.GetType(), level);
                    if (card != null)
                    {
                        candidates.Add(card);
                    }
                }
            }
            if (candidates.Count == 0)
            {
                return;
            }
            var spell = rng.NextItem(candidates);
            if (spell == null)
            {
                return;
            }
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(spell);
            jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(spell);

            // 随机目标：AnyEnemy 单体攻击牌除非描述限定"对敌人"，目标放宽为全部存活生物
            // （自己/队友角色、双方随从、敌人）；其余按卡合法性过滤（合法优先，回退全量）。
            var target = jaina.Scripts.Character.JainaRandomPoolHelper.PickRandomTarget(player, combatState, spell);
            if (spell.TargetType != TargetType.None && target == null)
            {
                return; // 无合法目标：不施放（惊奇卡牌也不消耗）
            }

            // 施放节奏与"倾泻"等自动打出卡一致：先进入打出区，停顿后再施放效果
            // （原版 AutoPlayFromDrawPile 先逐张 Add 到打出区再逐个 AutoPlay）
            if (spell.Pile == null)
            {
                await CardPileCmd.Add(spell, PileType.Play);
            }
            await Cmd.Wait(0.5f);
            await CardCmd.AutoPlay(choiceContext, spell, target, skipCardPileVisuals: true);

            // 释放后此卡消耗
            if (amazingCard.Pile != null && amazingCard.Pile.Type == PileType.Hand)
            {
                await CardPileCmd.Add(amazingCard, PileType.Exhaust);
            }
        }
        catch (Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] AmazingCard trigger failed: {ex}");
        }
    }
}
