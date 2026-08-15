using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character;

/// <summary>
/// 手牌容量辅助。
/// 0.111.1 手牌上限 10（CardPile.MaxCardsInHand）；满手时 CardPileCmd.Add 会把牌
/// 静默改道弃牌堆（CardPileCmd.cs 的 isFullHandAdd 分支），玩家只看到 "HAND_FULL" 气泡。
/// 生成/自动入手前先检查，避免"选中的牌消失/直接进弃牌堆"。
/// 英雄技能卡（HeroPower 关键词）不占手牌位：手牌容量只统计非英雄技能卡。
/// </summary>
public static class JainaHandHelper
{
    /// <summary>
    /// 手牌中是否已无空间（满手时入手会被改道弃牌堆）。
    /// 英雄技能卡不占位：仅非英雄技能卡计入上限。
    /// </summary>
    public static bool IsHandFull(Player? player)
    {
        var hand = player?.PlayerCombatState?.Hand;
        if (hand == null)
        {
            return false;
        }
        int nonHeroPowerCount = hand.Cards.Count(c => !Powers.HeroPowerHandHelper.IsHeroPowerCard(c));
        return nonHeroPowerCount >= CardPile.MaxCardsInHand;
    }

    /// <summary>
    /// 手牌是否有空间（满手返回 false）
    /// </summary>
    public static bool CanAddToHand(Player? player) => !IsHandFull(player);
}
