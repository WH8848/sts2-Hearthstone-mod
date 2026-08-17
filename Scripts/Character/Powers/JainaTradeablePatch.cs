using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using jaina.Scripts.Character.Keywords;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 交易（Tradeable）：将此卡牌拖到弃牌堆上方松手会洗入你的弃牌堆，
/// 然后你从抽牌堆抽一张牌。
/// 实现：拦截打出流程（NCardPlay.TryPlayCard）——若松手时鼠标位于弃牌堆 UI 上方
/// 且卡带"交易"关键词，则执行交易替代打出。
/// </summary>
[HarmonyPatch(typeof(NCardPlay), "TryPlayCard")]
public static class JainaTradeablePatch
{
    public static bool Prefix(NCardPlay __instance)
    {
        try
        {
            var card = __instance.Holder?.CardModel;
            if (card == null || card.Owner == null || card.Pile?.Type != PileType.Hand)
            {
                return true;
            }
            if (!card.Keywords.Contains(JainaKeywords.Tradeable))
            {
                return true;
            }
            // 松手时鼠标位于弃牌堆 UI 上方
            var mousePos = __instance.GetViewport()?.GetMousePosition();
            if (mousePos == null)
            {
                return true;
            }
            var discardPile = NCombatRoom.Instance?.Ui?.DiscardPile;
            if (discardPile == null || !discardPile.GetGlobalRect().HasPoint(mousePos.Value))
            {
                return true;
            }
            // 交易替代打出
            _ = Trade(card);
            return false;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// 交易：洗入弃牌堆，然后从抽牌堆抽一张牌（顶牌入手；无牌可抽不抽）。
    /// </summary>
    private static async Task Trade(CardModel card)
    {
        try
        {
            var player = card.Owner;
            if (player == null || player.Creature?.CombatState == null)
            {
                return;
            }
            // 洗入弃牌堆
            await CardPileCmd.Add(card, PileType.Discard);
            // 从抽牌堆抽一张牌（顶牌）
            var drawPile = player.PlayerCombatState?.DrawPile;
            if (drawPile != null && drawPile.Cards.Count > 0)
            {
                var top = drawPile.Cards.FirstOrDefault();
                if (top != null)
                {
                    await CardPileCmd.Add(top, PileType.Hand);
                }
            }
        }
        catch (Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] Tradeable trade failed: {ex}");
        }
    }
}
