using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// "自动打出"卡追踪：标记非玩家手打的卡实例（罗曼斯战吼重放、以及其他
/// CardCmd.AutoPlay 自动打出）。这些卡：
/// - 对吉安娜自己造成的伤害不触发随从军势挡伤（罗曼斯释放的额外法术）；
/// - 不计入"手打的牌库外法术"（打开时空之门等只计数玩家手打）。
/// 用 ConditionalWeakTable 弱引用，卡实例回收后自动清理，无内存泄漏。
/// </summary>
public static class RommathReplayTracker
{
    private static readonly ConditionalWeakTable<CardModel, object> Marked = new();

    /// <summary>标记一张卡为"自动打出"（非玩家手打）</summary>
    public static void Mark(CardModel card)
    {
        Marked.Remove(card);
        Marked.Add(card, null!);
    }

    /// <summary>该卡是否为"自动打出"（非玩家手打）</summary>
    public static bool IsMarked(CardModel card)
    {
        return Marked.TryGetValue(card, out _);
    }
}

/// <summary>
/// CardCmd.AutoPlay 统一标记：所有自动打出的卡（罗曼斯重放等）标记为"非手打"，
/// 供打开时空之门等"只计数手打牌库外法术"的判定排除。
/// </summary>
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.AutoPlay))]
public static class AutoPlayMarkPatch
{
    private static void Prefix(CardModel card)
    {
        if (card != null)
        {
            RommathReplayTracker.Mark(card);
        }
    }
}
