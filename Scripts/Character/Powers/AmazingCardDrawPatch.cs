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
        // 抽牌任务之后异步执行（LifecyclePatchTaskBridge 是 internal，这里直接启动异步触发）
        _ = Trigger(__1, card);
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

            // 全角色卡牌候选：所有攻击/技能牌（不含英雄技能卡），按可升级级别展开
            // （应用 Jaina 随机池统一排除：7 个非角色池/先古稀有度/多人专属）
            var candidates = ModelDb.AllCards
                .Where(c => c != null &&
                            (c.Type == CardType.Attack || c.Type == CardType.Skill) &&
                            !HeroPowerHandHelper.IsHeroPowerCard(c) &&
                            jaina.Scripts.Character.JainaRandomPoolHelper.IsEligible(c))
                .ToList();
            if (candidates.Count == 0)
            {
                return;
            }
            var chosen = rng.NextItem(candidates);
            if (chosen == null)
            {
                return;
            }
            int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(chosen.GetType());
            int upgradeLevel = rng.NextInt(0, maxLevel + 1);
            var spell = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, player, chosen.GetType(), upgradeLevel);
            if (spell == null)
            {
                return;
            }
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(spell);
            jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(spell);

            // 单目标牌：从场上所有活物中随机选一个合法目标
            Creature? target = null;
            if (spell.TargetType == TargetType.AnyEnemy || spell.TargetType == TargetType.AnyPlayer ||
                spell.TargetType == TargetType.AnyAlly ||
                (CustomTargetTypeManager.TryGetCustomTargetType(spell.TargetType, out var customType) &&
                 customType.IsSingleTarget))
            {
                var pool = combatState.Creatures
                    .Where(c => c != null && c.IsAlive && spell.IsValidTarget(c))
                    .ToList();
                target = pool.Count > 0 ? rng.NextItem(pool) : null;
                if (target == null)
                {
                    return; // 无合法目标：不施放（惊奇卡牌也不消耗）
                }
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
