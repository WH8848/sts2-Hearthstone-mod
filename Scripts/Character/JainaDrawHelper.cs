using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character;

/// <summary>
/// 定向抽牌辅助：从抽牌堆挑符合条件的牌，抽牌堆不足/条件不满足时从弃牌堆补足。
/// 所有"抽特定类型/特定费用的牌"统一语义：抽牌堆数量不够或条件不符合 → 从弃牌堆抽
/// （加大音量抽三张法术牌、任务线奖励抽一张法术牌、机器人召唤者拨号抽牌等）。
/// 普通抽牌由 CardPileCmd.Draw 处理（抽牌堆空 → 弃牌堆洗回），不走这里。
/// </summary>
public static class JainaDrawHelper
{
    /// <summary>
    /// 从抽牌堆中挑最多 count 张符合条件的牌；不足时从弃牌堆按顺序补足。
    /// 返回的卡仍在原牌堆中（调用方负责入手/移除）。
    /// </summary>
    public static List<CardModel> PickMatchingFromDrawThenDiscard(
        Player player, int count, Func<CardModel, bool> predicate)
    {
        var result = new List<CardModel>();
        if (player?.PlayerCombatState == null || count <= 0 || predicate == null)
        {
            return result;
        }
        foreach (var pile in new[]
                 {
                     player.PlayerCombatState.DrawPile,
                     player.PlayerCombatState.DiscardPile
                 })
        {
            if (result.Count >= count || pile == null)
            {
                break;
            }
            foreach (var card in pile.Cards)
            {
                if (result.Count >= count)
                {
                    break;
                }
                if (card != null && predicate(card))
                {
                    result.Add(card);
                }
            }
        }
        return result;
    }
}
