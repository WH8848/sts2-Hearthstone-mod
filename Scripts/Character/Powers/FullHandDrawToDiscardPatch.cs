using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 吉安娜满手抽牌 → 牌进弃牌堆（炉石语义"抽牌满手烧牌"变体：牌不消失、不消耗，直接进入弃牌堆）。
/// 原版 CardPileCmd.Draw 满手（非英雄技能计数 ≥ 上限）时不抽（牌留在抽牌堆）；
/// 炉石形态（HearthstoneFormPower）满手时通过 ShouldDraw 拦截并烧牌（牌被消耗）。
/// 本 Prefix 在 Draw 入口短路：吉安娜手牌满时自行抽取（抽牌堆 → 弃牌堆），
/// 同时覆盖炉石形态的满手烧牌拦截——满手抽到的牌一律进弃牌堆。
/// 只对吉安娜生效（其他角色保持原版行为）。
/// 联机：抽取顺序/洗牌复用原版命令，两端确定性一致。
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Draw),
    new Type[] { typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool) })]
public static class FullHandDrawToDiscardPatch
{
    private static bool Prefix(PlayerChoiceContext choiceContext, decimal count, Player player,
        bool fromHandDraw, ref Task<IEnumerable<CardModel>> __result)
    {
        // 只对吉安娜生效
        if (player?.Character is not jaina.Scripts.Character.Jaina)
        {
            return true;
        }
        // 手牌未满：原版抽牌流程
        if (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
        {
            return true;
        }
        // 吉安娜手牌满：抽到的牌进弃牌堆（短路原版 Draw 与炉石形态满手烧牌拦截）
        __result = DrawFullHandToDiscard(choiceContext, count, player, fromHandDraw);
        return false;
    }

    /// <summary>
    /// 满手抽牌：从抽牌堆取 count 张（抽牌堆空时弃牌堆洗回），逐张移入弃牌堆，
    /// 保留原版 Draw 的历史/钩子（CardDrawn/AfterCardDrawn/InvokeDrawn——炉石形态
    /// "抽到状态卡额外抽一张"仍生效）。
    /// </summary>
    private static async Task<IEnumerable<CardModel>> DrawFullHandToDiscard(
        PlayerChoiceContext choiceContext, decimal count, Player player, bool fromHandDraw)
    {
        var result = new List<CardModel>();
        int draws = Math.Max(0, (int)Math.Ceiling(count));
        var combatState = player.Creature?.CombatState;
        for (int i = 0; i < draws; i++)
        {
            if (combatState == null || CombatManager.Instance.IsOverOrEnding)
            {
                break;
            }
            var drawPile = PileType.Draw.GetPile(player);
            if (drawPile.Cards.Count == 0)
            {
                // 抽牌堆空：弃牌堆洗回（原版抽牌语义）；两堆都空 → 停止（疲劳由原版机制处理）
                if (PileType.Discard.GetPile(player).Cards.Count == 0)
                {
                    break;
                }
                await CardPileCmd.Shuffle(choiceContext, player);
            }
            var card = drawPile.Cards.FirstOrDefault();
            if (card == null)
            {
                break;
            }
            result.Add(card);
            card.RemoveFromCurrentPile(silent: true);
            await CardPileCmd.Add(card, PileType.Discard);
            // 与原版 Draw 一致：历史记录 + 抽牌钩子（炉石形态 AfterCardDrawn 连锁额外抽牌仍生效）
            CombatManager.Instance.History.CardDrawn(combatState, card, fromHandDraw);
            await Hook.AfterCardDrawn(combatState, choiceContext, card, fromHandDraw);
            card.InvokeDrawn();
        }
        return result;
    }
}
