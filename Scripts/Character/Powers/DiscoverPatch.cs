using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 发现识别 hook：所有发现（三选一入手）都经过
/// CardSelectCmd.FromChooseACardScreen（选择）+ CardPileCmd.AddGeneratedCardToCombat（入手）。
/// 在这两个入口记录/消费"发现完成"事件，供禁忌序列/源生之石使用。
/// </summary>
public static class DiscoverPatch
{
    /// <summary>
    /// 发现界面开始：记录进行中的候选
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseACardScreen))]
    public static class DiscoverBeginPatch
    {
        private static void Prefix(Player player, IReadOnlyList<CardModel> cards)
        {
            DiscoverTracker.BeginDiscover(player, cards);
        }
    }

    /// <summary>
    /// 发现卡置入手牌：若属于进行中候选，记为一次发现完成
    /// </summary>
    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardToCombat))]
    public static class DiscoverCompletePatch
    {
        private static void Prefix(CardModel card, Player? creator)
        {
            DiscoverTracker.OnCardAddedToHand(creator!, card);
        }
    }
}
