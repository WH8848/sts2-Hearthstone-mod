using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 英雄技能卡（HeroPower 关键词，如火焰冲击）不占手牌位：
/// 1. CardPileCmd.Add 的满手判定豁免——英雄技能卡永远可入手（不触发满手改道弃牌堆）；
///    其余卡判定时，手牌中已有的英雄技能卡不计入 10 张上限。
/// 2. CardPileCmd.Draw 的抽牌空间计算排除英雄技能卡——
///    手牌中英雄技能卡不占位，抽牌空间 = 上限 - 非英雄技能卡数。
/// 通过 Transpiler 修改原版 Add / DrawInternal 状态机实现。
/// </summary>
public static class HeroPowerHandHelper
{
    /// <summary>
    /// 是否为英雄技能卡（带 HeroPower 关键词）
    /// </summary>
    public static bool IsHeroPowerCard(CardModel card)
    {
        return card != null && card.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower);
    }

    /// <summary>
    /// 手牌中非英雄技能卡数量（英雄技能卡不占位）
    /// </summary>
    public static int GetNonHeroPowerCardCount(CardPile hand)
    {
        int count = 0;
        foreach (var c in hand.Cards)
        {
            if (!IsHeroPowerCard(c))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Add 满手判定（替代原版 cardPile.Cards.Count >= MaxCardsInHand）：
    /// 英雄技能卡永不视为满手；其余卡按非英雄技能卡数 >= 上限 判定。
    /// </summary>
    public static bool IsFullHandAdd(CardPile hand, CardModel card)
    {
        // 英雄技能卡不占手牌位：永远可入手
        if (IsHeroPowerCard(card))
        {
            return false;
        }
        return GetNonHeroPowerCardCount(hand) >= CardPile.MaxCardsInHand;
    }
}

/// <summary>
/// CardPileCmd.Add（批量核心重载）状态机：满手判定替换为 IsFullHandAdd。
/// 原 IL：
///   ldloc.s 16 (cardPile); callvirt get_Cards(); callvirt get_Count();
///   call get_MaxCardsInHand(); clt; ldc.i4.0; ceq; br.s -> stfld isFullHandAdd
/// 替换为：
///   ldloc.s 16 (cardPile); ldarg.0; ldfld &lt;card&gt;5__5; call IsFullHandAdd(CardPile, CardModel); br.s
/// </summary>
[HarmonyPatch]
public static class HeroPowerHandAddPatch
{
    private static readonly MethodInfo GetCards =
        AccessTools.PropertyGetter(typeof(CardPile), nameof(CardPile.Cards));

    private static readonly MethodInfo GetCount =
        typeof(System.Collections.Generic.IReadOnlyCollection<CardModel>).GetMethod("get_Count")!;

    private static readonly MethodInfo GetMaxCardsInHand =
        AccessTools.PropertyGetter(typeof(CardPile), nameof(CardPile.MaxCardsInHand));

    private static readonly MethodInfo IsFullHandAdd =
        AccessTools.Method(typeof(HeroPowerHandHelper), nameof(HeroPowerHandHelper.IsFullHandAdd));

    private static MethodBase TargetMethod()
    {
        // 定位 Add 核心重载的状态机：含 <isFullHandAdd> 字段的 <Add>d__* 嵌套类
        foreach (var type in typeof(CardPileCmd).GetNestedTypes(BindingFlags.NonPublic))
        {
            if (!type.Name.StartsWith("<Add>d__", System.StringComparison.Ordinal))
            {
                continue;
            }
            if (type.GetField("<isFullHandAdd>5__6", BindingFlags.Instance | BindingFlags.NonPublic) != null)
            {
                return type.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }
        return null;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        for (int i = 0; i < codes.Count; i++)
        {
            // 匹配：callvirt get_Cards + callvirt get_Count + call get_MaxCardsInHand + clt + ldc.i4.0 + ceq
            if (codes[i].opcode == OpCodes.Callvirt && Equals(codes[i].operand, GetCards) &&
                i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Callvirt && Equals(codes[i + 1].operand, GetCount) &&
                i + 2 < codes.Count && codes[i + 2].opcode == OpCodes.Call && Equals(codes[i + 2].operand, GetMaxCardsInHand) &&
                i + 3 < codes.Count && codes[i + 3].opcode == OpCodes.Clt &&
                i + 4 < codes.Count && codes[i + 4].opcode == OpCodes.Ldc_I4_0 &&
                i + 5 < codes.Count && codes[i + 5].opcode == OpCodes.Ceq)
            {
                // 前一条应为 ldloc（cardPile）
                int ldlocIdx = i - 1;
                if (ldlocIdx < 0 || !IsLdloc(codes[ldlocIdx].opcode))
                {
                    continue;
                }
                var cardPileLoad = codes[ldlocIdx];

                // 状态机中的当前卡字段 <card>5__5
                var stateMachine = TargetMethod()!.DeclaringType!;
                var cardField = stateMachine.GetField("<card>5__5", BindingFlags.Instance | BindingFlags.NonPublic);
                if (cardField == null)
                {
                    continue;
                }
                codes.RemoveRange(ldlocIdx, i + 6 - ldlocIdx);
                codes.InsertRange(ldlocIdx, new[]
                {
                    cardPileLoad,
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, cardField),
                    new CodeInstruction(OpCodes.Call, IsFullHandAdd)
                });
                break;
            }
        }
        return codes;
    }

    private static bool IsLdloc(OpCode opcode)
    {
        return opcode == OpCodes.Ldloc || opcode == OpCodes.Ldloc_S ||
               opcode == OpCodes.Ldloc_0 || opcode == OpCodes.Ldloc_1 ||
               opcode == OpCodes.Ldloc_2 || opcode == OpCodes.Ldloc_3;
    }
}

/// <summary>
/// CardPileCmd.Draw 内部（DrawInternal）状态机：抽牌空间计算排除英雄技能卡。
/// 原 IL（3 处）：ldfld &lt;hand&gt;5__4; callvirt get_Cards(); callvirt get_Count()
/// 替换为：ldfld &lt;hand&gt;5__4; call GetNonHeroPowerCardCount(CardPile)
/// </summary>
[HarmonyPatch]
public static class HeroPowerHandDrawPatch
{
    private static readonly MethodInfo GetCards =
        AccessTools.PropertyGetter(typeof(CardPile), nameof(CardPile.Cards));

    private static readonly MethodInfo GetCount =
        typeof(System.Collections.Generic.IReadOnlyCollection<CardModel>).GetMethod("get_Count")!;

    private static readonly MethodInfo NonHeroCount =
        AccessTools.Method(typeof(HeroPowerHandHelper), nameof(HeroPowerHandHelper.GetNonHeroPowerCardCount));

    private static MethodBase TargetMethod()
    {
        // 定位 Draw 内部状态机：含 <hand>5__4 字段的 <DrawInternal>d__* 嵌套类
        foreach (var type in typeof(CardPileCmd).GetNestedTypes(BindingFlags.NonPublic))
        {
            if (!type.Name.StartsWith("<DrawInternal>d__", System.StringComparison.Ordinal))
            {
                continue;
            }
            if (type.GetField("<hand>5__4", BindingFlags.Instance | BindingFlags.NonPublic) != null)
            {
                return type.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }
        return null;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        for (int i = 0; i < codes.Count; i++)
        {
            // 匹配：callvirt get_Cards + callvirt get_Count（且前一条是 ldfld <hand>5__4 或 drawPile 字段）
            if (codes[i].opcode == OpCodes.Callvirt && Equals(codes[i].operand, GetCards) &&
                i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Callvirt && Equals(codes[i + 1].operand, GetCount))
            {
                int prev = i - 1;
                // 前一条应为 ldfld（hand / drawPile 等 CardPile 字段）
                if (prev < 0 || codes[prev].opcode != OpCodes.Ldfld)
                {
                    continue;
                }
                var pileField = codes[prev];
                codes.RemoveRange(prev, i + 2 - prev);
                codes.InsertRange(prev, new[]
                {
                    pileField,
                    new CodeInstruction(OpCodes.Call, NonHeroCount)
                });
                // 回退到替换点重新扫描，避免漏掉后续匹配
                i = prev;
            }
        }
        return codes;
    }
}

/// <summary>
/// CardPileCmd.CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot：
/// "无法抽牌"判断同样排除英雄技能卡（手牌中英雄技能卡不占位）。
/// 原版逻辑：抽牌堆+弃牌堆为空 或 手牌数 >= 上限 → 无法抽牌并弹气泡。
/// 替换为：手牌中非英雄技能卡数 >= 上限 → 无法抽牌。
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), "CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot")]
public static class HeroPowerHandDrawPossiblePatch
{
    private static bool Prefix(Player player, ref bool __result)
    {
        if (PileType.Draw.GetPile(player).Cards.Count + PileType.Discard.GetPile(player).Cards.Count == 0)
        {
            ThinkCmd.Play(new LocString("combat_messages", "NO_DRAW"), player.Creature, 2.0);
            __result = false;
            return false;
        }
        if (HeroPowerHandHelper.GetNonHeroPowerCardCount(PileType.Hand.GetPile(player)) >= CardPile.MaxCardsInHand)
        {
            ThinkCmd.Play(new LocString("combat_messages", "HAND_FULL"), player.Creature, 2.0);
            __result = false;
            return false;
        }
        __result = true;
        return false;
    }
}
