using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character;

/// <summary>
/// Jaina 随机卡牌池通用过滤（所有随机取卡统一使用）：
/// 排除 7 个非角色卡池（无色/诅咒/先古/状态/任务/事件/衍生）、
/// 先古稀有度（CardRarity.Ancient）与多人游戏专属卡（MultiplayerConstraint != None）。
/// 应用于：匣中古神/谜之匣、惊奇卡牌、戏法图腾、能量塑形师、惊奇套牌、旅社谍战等
/// 从 ModelDb.AllCards / AllCharacterCardPools 随机取卡的所有位置。
/// </summary>
public static class JainaRandomPoolHelper
{
    /// <summary>
    /// 被排除的非角色卡池类型
    /// （无色/诅咒/先古/状态/任务/事件/衍生池）
    /// </summary>
    private static readonly HashSet<Type> ExcludedPoolTypes =
    [
        typeof(MegaCrit.Sts2.Core.Models.CardPools.ColorlessCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.CurseCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.DeprecatedCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.StatusCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.QuestCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.EventCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.TokenCardPool)
    ];

    /// <summary>
    /// 该 canonical 卡是否可进入 Jaina 随机卡牌池：
    /// 不属于 7 个非角色池、不是先古稀有度、不是多人游戏专属卡。
    /// </summary>
    public static bool IsEligible(CardModel? canonical)
    {
        if (canonical == null)
        {
            return false;
        }
        if (IsInExcludedPool(canonical))
        {
            return false;
        }
        if (canonical.Rarity == CardRarity.Ancient)
        {
            return false;
        }
        if (canonical.MultiplayerConstraint != CardMultiplayerConstraint.None)
        {
            return false;
        }
        return true;
    }

    private static bool IsInExcludedPool(CardModel canonical)
    {
        foreach (var pool in ModelDb.AllCardPools)
        {
            if (pool == null || !ExcludedPoolTypes.Contains(pool.GetType()))
            {
                continue;
            }
            if (pool.AllCards.Contains(canonical))
            {
                return true;
            }
        }
        return false;
    }
}
