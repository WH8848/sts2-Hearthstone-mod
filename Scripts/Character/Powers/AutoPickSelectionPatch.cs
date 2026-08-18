using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 随机释放过程中<b>玩家不可操作</b>：匣中古神/谜之匣、惊奇卡牌、戏法图腾、大法师的符文、
/// 终极索兰莉安、冰血哨塔以及各种"再次释放"（诈骗犯/鹦鹉/罗曼斯等）随机释放（AutoPlay）的卡，
/// 遇到玩家选择机制（发现三选一/从手牌或牌堆选卡/弃牌选择/升级选择等）时——
/// <b>自动随机选择</b>，不弹界面、不暂停等待玩家。
/// 所有选择入口统一 patch：AutoPlay 上下文（调用栈检测 <see cref="AutoPlayGuard.IsInAutoPlay"/>）
/// 下 Prefix 直接随机选并跳过原方法。
/// 联机确定性：随机用 CombatTargets RNG，两端同步执行同一 AutoPlay 流程 → 结果一致。
/// </summary>
public static class AutoPickSelectionPatch
{
    /// <summary>
    /// 发现三选一（FromChooseACardScreen）：随机选一张候选入手（不弹发现界面）
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseACardScreen))]
    public static class DiscoverPatch
    {
        private static bool Prefix(Player player, IReadOnlyList<CardModel> cards, ref CardModel? __result)
        {
            if (!AutoPlayGuard.IsInAutoPlay())
            {
                return true;
            }
            __result = AutoPlayGuard.PickRandom(player, cards);
            return false;
        }
    }

    /// <summary>
    /// 从手牌选择（FromHand）：随机选 MinSelect 张
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHand))]
    public static class FromHandPatch
    {
        private static bool Prefix(Player player, CardSelectorPrefs prefs,
            Func<CardModel, bool>? filter, ref IEnumerable<CardModel> __result)
        {
            if (!AutoPlayGuard.IsInAutoPlay())
            {
                return true;
            }
            __result = AutoPlayGuard.PickRandomN(
                player, player.PlayerCombatState?.Hand?.Cards, prefs.MinSelect, filter);
            return false;
        }
    }

    /// <summary>
    /// 从手牌选一张弃置（FromHandForDiscard，如原版投掷匕首）：随机选 MinSelect 张弃置
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHandForDiscard))]
    public static class FromHandForDiscardPatch
    {
        private static bool Prefix(Player player, CardSelectorPrefs prefs,
            Func<CardModel, bool>? filter, ref IEnumerable<CardModel> __result)
        {
            if (!AutoPlayGuard.IsInAutoPlay())
            {
                return true;
            }
            __result = AutoPlayGuard.PickRandomN(
                player, player.PlayerCombatState?.Hand?.Cards, prefs.MinSelect, filter);
            return false;
        }
    }

    /// <summary>
    /// 从手牌选一张升级（FromHandForUpgrade）：随机选一张手牌
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHandForUpgrade))]
    public static class FromHandForUpgradePatch
    {
        private static bool Prefix(Player player, ref CardModel? __result)
        {
            if (!AutoPlayGuard.IsInAutoPlay())
            {
                return true;
            }
            __result = AutoPlayGuard.PickRandom(player, player.PlayerCombatState?.Hand?.Cards);
            return false;
        }
    }

    /// <summary>
    /// 从战斗牌堆选择（FromCombatPile，如原版头槌"从弃牌堆选一张回手"）：随机选 MinSelect 张
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromCombatPile),
        new Type[] { typeof(PlayerChoiceContext), typeof(CardPile), typeof(Player), typeof(CardSelectorPrefs), typeof(Func<CardModel, bool>) })]
    public static class FromCombatPilePatch
    {
        private static bool Prefix(CardPile pile, Player player, CardSelectorPrefs prefs,
            Func<CardModel, bool>? filter, ref IEnumerable<CardModel> __result)
        {
            if (!AutoPlayGuard.IsInAutoPlay())
            {
                return true;
            }
            __result = AutoPlayGuard.PickRandomN(player, pile?.Cards, prefs.MinSelect, filter);
            return false;
        }
    }

    /// <summary>
    /// 从一组卡中选择（FromChooseABundleScreen）：每组随机选一张
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseABundleScreen))]
    public static class FromChooseABundleScreenPatch
    {
        private static bool Prefix(Player player, IReadOnlyList<IReadOnlyList<CardModel>> bundles,
            ref IEnumerable<CardModel> __result)
        {
            if (!AutoPlayGuard.IsInAutoPlay())
            {
                return true;
            }
            var picked = new List<CardModel>();
            if (bundles != null)
            {
                foreach (var bundle in bundles)
                {
                    var card = AutoPlayGuard.PickRandom(player, bundle);
                    if (card != null)
                    {
                        picked.Add(card);
                    }
                }
            }
            __result = picked;
            return false;
        }
    }

    /// <summary>
    /// 简单网格选择（FromSimpleGrid）：随机选 MinSelect 张
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromSimpleGrid))]
    public static class FromSimpleGridPatch
    {
        private static bool Prefix(IReadOnlyList<CardModel> cardsIn, Player player,
            CardSelectorPrefs prefs, ref IEnumerable<CardModel> __result)
        {
            if (!AutoPlayGuard.IsInAutoPlay())
            {
                return true;
            }
            __result = AutoPlayGuard.PickRandomN(player, cardsIn, prefs.MinSelect);
            return false;
        }
    }
}
