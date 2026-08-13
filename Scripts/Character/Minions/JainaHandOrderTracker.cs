using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 露娜手牌顺序追踪（按玩家共享）：所有露娜实例共用同一份手牌顺序快照，
/// 保证多只露娜的"最右"判定完全一致（列表末尾 = 最右），
/// 也避免各实例独立快照因挂载时机不同而漂移。
/// </summary>
public static class JainaHandOrderTracker
{
    private static readonly ConditionalWeakTable<Player, List<CardModel>> Orders = new();

    public static List<CardModel> For(Player player) => Orders.GetValue(player, _ => new List<CardModel>());

    /// <summary>
    /// 重建快照（回合开始/召唤时用当前手牌覆盖；多只露娜重复调用幂等）
    /// </summary>
    public static void Rebuild(Player player, IReadOnlyList<CardModel>? hand)
    {
        var list = For(player);
        list.Clear();
        if (hand != null)
        {
            list.AddRange(hand);
        }
    }

    /// <summary>
    /// 追加到末尾（抽牌/生成牌入手；幂等防重复 append）
    /// </summary>
    public static void Append(Player player, CardModel card)
    {
        var list = For(player);
        if (!list.Contains(card))
        {
            list.Add(card);
        }
    }

    /// <summary>
    /// 打出时定位并移除，返回是否为最右一张
    /// </summary>
    public static bool TryConsume(Player player, CardModel card)
    {
        var list = For(player);
        int index = list.IndexOf(card);
        if (index < 0)
        {
            return false;
        }
        bool rightmost = index == list.Count - 1;
        list.RemoveAt(index);
        return rightmost;
    }

    /// <summary>
    /// 兜底移除（AfterCardPlayed 清理残留；幂等）
    /// </summary>
    public static void Remove(Player player, CardModel card) => For(player).Remove(card);
}
