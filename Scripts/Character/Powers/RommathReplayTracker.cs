using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 罗曼斯重放卡追踪：标记"罗曼斯战吼额外重放的卡"实例。
/// 这些额外法术对吉安娜自己造成的伤害不触发随从军势挡伤
/// （罗曼斯释放的额外法术是免费自动打出，其伤害直接生效）。
/// 用 ConditionalWeakTable 弱引用，卡实例回收后自动清理，无内存泄漏。
/// </summary>
public static class RommathReplayTracker
{
    private static readonly ConditionalWeakTable<CardModel, object> Marked = new();

    /// <summary>标记一张卡为"罗曼斯重放卡"</summary>
    public static void Mark(CardModel card)
    {
        Marked.Remove(card);
        Marked.Add(card, null!);
    }

    /// <summary>该卡是否为"罗曼斯重放卡"</summary>
    public static bool IsMarked(CardModel card)
    {
        return Marked.TryGetValue(card, out _);
    }
}
