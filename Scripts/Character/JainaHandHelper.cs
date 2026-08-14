using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

namespace jaina.Scripts.Character;

/// <summary>
/// 手牌容量辅助。
/// 0.111.1 手牌上限 10（CardPile.MaxCardsInHand）；满手时 CardPileCmd.Add 会把牌
/// 静默改道弃牌堆（CardPileCmd.cs 的 isFullHandAdd 分支），玩家只看到 "HAND_FULL" 气泡。
/// 生成/自动入手前先检查，避免"选中的牌消失/直接进弃牌堆"。
/// </summary>
public static class JainaHandHelper
{
    /// <summary>
    /// 玩家手牌是否已满（满手时入手会被改道弃牌堆）
    /// </summary>
    public static bool IsHandFull(Player? player)
    {
        return player?.PlayerCombatState?.Hand?.Cards.Count >= CardPile.MaxCardsInHand;
    }

    /// <summary>
    /// 手牌是否有空间（满手返回 false）
    /// </summary>
    public static bool CanAddToHand(Player? player) => !IsHandFull(player);
}
