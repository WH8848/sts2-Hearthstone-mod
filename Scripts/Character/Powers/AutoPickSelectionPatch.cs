using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
/// 遇到<b>任何</b>玩家选择机制（发现三选一/从手牌或牌堆选卡/弃牌/升级/变形/附魔/移除/奖励选择等）时——
/// <b>自动随机选择</b>，不弹界面、不暂停等待玩家（炉石语义：随机释放的卡不打断操作）。
/// 所有 CardSelectCmd 选择入口统一 patch：AutoPlay 上下文
/// （<see cref="AutoPlayGuard.IsAutoPlayContext"/>：调用栈检测 + 当前 AutoPlay 卡实例标记双保险）
/// 下 Prefix 直接随机选并跳过原方法。
/// 注意：这些方法都是 <b>async</b>——Harmony 对 async 方法的 __result 类型是 Task&lt;T&gt;，
/// Prefix 必须用 Task&lt;T&gt; 类型并通过 Task.FromResult 返回（否则 PatchAll 抛
/// "Cannot assign method return type Task... to __result"）。
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
        private static bool Prefix(Player player, IReadOnlyList<CardModel> cards,
            ref Task<CardModel?> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(null))
            {
                return true;
            }
            __result = Task.FromResult(AutoPlayGuard.PickRandom(player, cards));
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
            Func<CardModel, bool>? filter, AbstractModel source, ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(source as CardModel))
            {
                return true;
            }
            __result = Task.FromResult<IEnumerable<CardModel>>(AutoPlayGuard.PickRandomN(
                player, player.PlayerCombatState?.Hand?.Cards, prefs.MinSelect, filter));
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
            Func<CardModel, bool>? filter, AbstractModel source, ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(source as CardModel))
            {
                return true;
            }
            __result = Task.FromResult<IEnumerable<CardModel>>(AutoPlayGuard.PickRandomN(
                player, player.PlayerCombatState?.Hand?.Cards, prefs.MinSelect, filter));
            return false;
        }
    }

    /// <summary>
    /// 从手牌选一张升级（FromHandForUpgrade）：随机选一张手牌
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromHandForUpgrade))]
    public static class FromHandForUpgradePatch
    {
        private static bool Prefix(Player player, AbstractModel source, ref Task<CardModel?> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(source as CardModel))
            {
                return true;
            }
            __result = Task.FromResult(AutoPlayGuard.PickRandom(player, player.PlayerCombatState?.Hand?.Cards));
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
            Func<CardModel, bool>? filter, ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(null))
            {
                return true;
            }
            __result = Task.FromResult<IEnumerable<CardModel>>(AutoPlayGuard.PickRandomN(
                player, pile?.Cards, prefs.MinSelect, filter));
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
            ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(null))
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
            __result = Task.FromResult<IEnumerable<CardModel>>(picked);
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
            CardSelectorPrefs prefs, ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(null))
            {
                return true;
            }
            __result = Task.FromResult<IEnumerable<CardModel>>(AutoPlayGuard.PickRandomN(
                player, cardsIn, prefs.MinSelect));
            return false;
        }
    }

    /// <summary>
    /// 从牌组选择（FromDeckGeneric）：随机选 MinSelect 张
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckGeneric))]
    public static class FromDeckGenericPatch
    {
        private static bool Prefix(Player player, CardSelectorPrefs prefs,
            Func<CardModel, bool>? filter, ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(null))
            {
                return true;
            }
            __result = Task.FromResult<IEnumerable<CardModel>>(AutoPlayGuard.PickRandomN(
                player, PileType.Deck.GetPile(player)?.Cards, prefs.MinSelect, filter));
            return false;
        }
    }

    /// <summary>
    /// 从牌组选一张升级（FromDeckForUpgrade）：随机选 MinSelect 张
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForUpgrade))]
    public static class FromDeckForUpgradePatch
    {
        private static bool Prefix(Player player, CardSelectorPrefs prefs,
            ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(null))
            {
                return true;
            }
            __result = Task.FromResult<IEnumerable<CardModel>>(AutoPlayGuard.PickRandomN(
                player, PileType.Deck.GetPile(player)?.Cards, prefs.MinSelect));
            return false;
        }
    }

    /// <summary>
    /// 从牌组选一张变形（FromDeckForTransformation）：随机选 MinSelect 张
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForTransformation))]
    public static class FromDeckForTransformationPatch
    {
        private static bool Prefix(Player player, CardSelectorPrefs prefs,
            ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(null))
            {
                return true;
            }
            __result = Task.FromResult<IEnumerable<CardModel>>(AutoPlayGuard.PickRandomN(
                player, PileType.Deck.GetPile(player)?.Cards, prefs.MinSelect));
            return false;
        }
    }

    /// <summary>
    /// 从牌组选一张附魔（FromDeckForEnchantment，Player 版）：随机选 MinSelect 张
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForEnchantment),
        new Type[] { typeof(Player), typeof(EnchantmentModel), typeof(int), typeof(CardSelectorPrefs) })]
    public static class FromDeckForEnchantmentPatch
    {
        private static bool Prefix(Player player, CardSelectorPrefs prefs,
            ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(null))
            {
                return true;
            }
            __result = Task.FromResult<IEnumerable<CardModel>>(AutoPlayGuard.PickRandomN(
                player, PileType.Deck.GetPile(player)?.Cards, prefs.MinSelect));
            return false;
        }
    }

    /// <summary>
    /// 从牌组选一张移除（FromDeckForRemoval）：随机选 MinSelect 张
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForRemoval))]
    public static class FromDeckForRemovalPatch
    {
        private static bool Prefix(Player player, CardSelectorPrefs prefs,
            Func<CardModel, bool>? filter, ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(null))
            {
                return true;
            }
            __result = Task.FromResult<IEnumerable<CardModel>>(AutoPlayGuard.PickRandomN(
                player, PileType.Deck.GetPile(player)?.Cards, prefs.MinSelect, filter));
            return false;
        }
    }

    /// <summary>
    /// 从牌组选一张附魔（FromDeckForEnchantment，cards 版——Player 版委托此版本，独立实现）：
    /// 随机选 MinSelect 张
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromDeckForEnchantment),
        new Type[] { typeof(IReadOnlyList<CardModel>), typeof(EnchantmentModel), typeof(int), typeof(CardSelectorPrefs) })]
    public static class FromDeckForEnchantmentCardsPatch
    {
        private static bool Prefix(IReadOnlyList<CardModel> cards, CardSelectorPrefs prefs,
            ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(null))
            {
                return true;
            }
            var owner = cards is { Count: > 0 } ? cards[0].Owner : null;
            __result = Task.FromResult<IEnumerable<CardModel>>(AutoPlayGuard.PickRandomN(
                owner, cards, prefs.MinSelect));
            return false;
        }
    }

    /// <summary>
    /// 奖励网格选择（FromSimpleGridForRewards）：随机选 MinSelect 张（防御性——随机释放不会触发奖励）
    /// </summary>
    [HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromSimpleGridForRewards))]
    public static class FromSimpleGridForRewardsPatch
    {
        private static bool Prefix(Player player, List<CardCreationResult> cards,
            CardSelectorPrefs prefs, ref Task<IEnumerable<CardModel>> __result)
        {
            if (!AutoPlayGuard.IsAutoPlayContext(null))
            {
                return true;
            }
            var picked = new List<CardModel>();
            if (cards != null)
            {
                foreach (var r in cards)
                {
                    if (r?.Card != null)
                    {
                        picked.Add(r.Card);
                    }
                }
            }
            __result = Task.FromResult<IEnumerable<CardModel>>(AutoPlayGuard.PickRandomN(
                player, picked, prefs.MinSelect));
            return false;
        }
    }
}
