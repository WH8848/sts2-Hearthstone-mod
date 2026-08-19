using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 微缩（Miniaturize）：当你使用带有"微缩"的随从牌后（仅从手牌打出触发；
/// 召唤/复活登场不触发），立即将一张 0 费 1/1 的复制（微型）置入你的手牌。
/// 微型复制品完整保留原卡牌的所有文字效果，带"微型"关键词、去掉"微缩"（不再触发），
/// 不消耗（打出后进弃牌堆，可再次抽回）。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), "AfterCardPlayed",
    new[] { typeof(ICombatState), typeof(PlayerChoiceContext), typeof(CardPlay) })]
public static class JainaMiniaturizePatch
{
    public static void Postfix(ICombatState __0, PlayerChoiceContext __1, CardPlay __2)
    {
        // 只在从手牌使用时触发（自动打出——召唤/复活等——不触发）
        if (__2.IsAutoPlay)
        {
            return;
        }
        var card = __2.Card;
        if (card == null || card.Owner == null)
        {
            return;
        }
        // 只对带"微缩"关键词的随从牌生效
        if (!card.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Miniaturize))
        {
            return;
        }
        // 串行执行：同一时刻可能打出多张带微缩的随从牌，多个并发异步任务的
        // await 恢复时序在两端机器上可能不同 → 微型复制品入手的顺序两端相反
        // → 手牌顺序分歧 → StateDivergence 断联。按打出顺序排队逐个执行
        // （打出顺序两端确定一致）。
        JainaSerialExecutor.Enqueue(__1, card, Trigger);
    }

    private static async Task Trigger(PlayerChoiceContext choiceContext, CardModel card)
    {
        try
        {
            var player = card.Owner;
            if (player == null || player.Creature?.CombatState == null)
            {
                return;
            }
            var combatState = player.Creature.CombatState;

            // 手牌满时 AddGeneratedCardToCombat 自动改道弃牌堆（原版满手语义，牌不消失不消耗）

            // 生成 0 费 1/1 的微型复制品（保留升级级别与全部文字效果）
            var copy = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, player, card.GetType(), card.CurrentUpgradeLevel);
            if (copy == null)
            {
                return;
            }
            copy.EnergyCost.SetCustomBaseCost(0);
            if (copy is jaina.Scripts.Character.Cards.JainaMinionCardTemplate minionCard)
            {
                minionCard.SetOverrideStats(1, 1);
            }
            // 关键词：去掉"微缩"（微型不再触发微缩），加上"微型"；不打消耗标记
            copy.RemoveKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Miniaturize);
            copy.AddKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Mini);
            copy.RemoveKeyword(CardKeyword.Exhaust);

            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(copy);
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, player);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] Miniaturize trigger failed: {ex}");
        }
    }
}
