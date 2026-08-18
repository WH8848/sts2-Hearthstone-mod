using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 随从生物类型 → 随从/地标卡牌类型的映射（悬停显示卡牌时获取本地化文本、
/// 随从种族判定、随机召唤池用）。
/// 映射<b>动态构建</b>：首次访问时遍历吉安娜卡池（JainaCardPool）与
/// 吉安娜中立/衍生池（JainaNeutralCardPool）的所有卡，
/// 从每张随从卡/地标卡的 SummonedMinionType / SummonedLandmarkType 反向建立映射——
/// 新增随从/地标卡无需手动维护映射表。
/// </summary>
public static class JainaMinionCardMap
{
    private static Dictionary<Type, Type>? _cardByMinionType;

    /// <summary>
    /// 惰性构建随从/地标类型 → 卡牌类型映射（ModelDb 卡池注册完成后调用，
    /// 首次访问时构建一次；主线程调用，无需加锁）。
    /// </summary>
    private static void EnsureBuilt()
    {
        if (_cardByMinionType != null)
        {
            return;
        }
        var map = new Dictionary<Type, Type>();
        var pools = new MegaCrit.Sts2.Core.Models.CardPoolModel[]
        {
            ModelDb.CardPool<JainaCardPool>(),
            ModelDb.CardPool<jaina.Scripts.Character.JainaNeutralCardPool>()
        };
        foreach (var pool in pools)
        {
            if (pool == null)
            {
                continue;
            }
            foreach (var canonical in pool.AllCards)
            {
                if (canonical == null)
                {
                    continue;
                }
                var cardType = canonical.GetType();
                switch (canonical)
                {
                    case JainaMinionCardTemplate minionCard:
                        // 随从卡：按卡声明的召唤随从类型映射（含中立池的衍生随从）
                        map[minionCard.SummonedMinionType] = cardType;
                        break;
                    case JainaLandmarkCardTemplate landmarkCard:
                        // 地标卡：按卡声明的召唤地标类型映射（不进入 MinionTypes 随机召唤池，
                        // 由 IsLandmarkType 区分）
                        map[landmarkCard.SummonedLandmarkType] = cardType;
                        break;
                }
            }
        }
        _cardByMinionType = map;
    }

    /// <summary>
    /// 取随从生物对应的卡牌类型（未映射返回 null）
    /// </summary>
    public static Type? GetCardType(Type minionType)
    {
        EnsureBuilt();
        return _cardByMinionType!.TryGetValue(minionType, out var cardType) ? cardType : null;
    }

    /// <summary>
    /// 全部随从生物类型（含地标；随机召唤池如需排除地标请过滤 <see cref="IsLandmarkType"/>）
    /// </summary>
    public static IEnumerable<Type> MinionTypes
    {
        get
        {
            EnsureBuilt();
            return _cardByMinionType!.Keys;
        }
    }

    /// <summary>
    /// 该随从生物类型是否为地标（地标不进入随机召唤池）
    /// </summary>
    public static bool IsLandmarkType(Type minionType)
    {
        return typeof(JainaLandmarkBase).IsAssignableFrom(minionType);
    }
}
