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
    public static int GetNonHeroPowerCardCountFromPile(CardPile hand)
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
    /// 卡牌集合中非英雄技能卡数量（CrashLanding 用 CardPile.GetCards(...).Count() 模式）
    /// </summary>
    public static int GetNonHeroPowerCardCountFromCards(IEnumerable<CardModel> cards)
    {
        int count = 0;
        foreach (var c in cards)
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
        return GetNonHeroPowerCardCountFromPile(hand) >= CardPile.MaxCardsInHand;
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
        AccessTools.Method(typeof(HeroPowerHandHelper), nameof(HeroPowerHandHelper.GetNonHeroPowerCardCountFromPile));

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
        if (HeroPowerHandHelper.GetNonHeroPowerCardCountFromPile(PileType.Hand.GetPile(player)) >= CardPile.MaxCardsInHand)
        {
            ThinkCmd.Play(new LocString("combat_messages", "HAND_FULL"), player.Creature, 2.0);
            __result = false;
            return false;
        }
        __result = true;
        return false;
    }
}

/// <summary>
/// 原版"抽牌/加牌到手牌满"卡（潦草急就 Scrawl、受膏 Anointed、坠落 CrashLanding、
/// 疏浚 Dredge、尼奥的怒火 NeowsFury）的 OnPlay：`MaxCardsInHand - 手牌总数` 的
/// 空间计算同样排除英雄技能卡（英雄技能卡不占手牌位）。
/// 模式 A（Scrawl/Anointed/Dredge/NeowsFury）：...get_Hand()/GetPile(Hand) → get_Cards → get_Count
///   替换 get_Cards + get_Count 为 GetNonHeroPowerCardCount(CardPile)
/// 模式 B（CrashLanding）：GetCards(owner, Hand) → Enumerable.Count
///   替换 Enumerable.Count 为 GetNonHeroPowerCardCount(IEnumerable)
/// </summary>
[HarmonyPatch]
public static class HeroPowerHandFullDrawCardPatch
{
    private static readonly MethodInfo GetCards =
        AccessTools.PropertyGetter(typeof(CardPile), nameof(CardPile.Cards));

    private static readonly MethodInfo GetCount =
        typeof(System.Collections.Generic.IReadOnlyCollection<CardModel>).GetMethod("get_Count")!;

    private static readonly MethodInfo CardPileGetCards =
        AccessTools.Method(typeof(CardPile), nameof(CardPile.GetCards));

    private static readonly MethodInfo EnumerableCount =
        typeof(System.Linq.Enumerable).GetMethods()
            .First(m => m.Name == "Count" && m.GetParameters().Length == 1 && m.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(CardModel));

    private static readonly MethodInfo NonHeroCountFromPile =
        AccessTools.Method(typeof(HeroPowerHandHelper), nameof(HeroPowerHandHelper.GetNonHeroPowerCardCountFromPile));

    private static readonly MethodInfo NonHeroCountFromEnumerable =
        AccessTools.Method(typeof(HeroPowerHandHelper), nameof(HeroPowerHandHelper.GetNonHeroPowerCardCountFromCards));

    /// <summary>
    /// 原版"抽牌/加牌到手牌满"卡（潦草急就 Scrawl、受膏 Anointed、坠落 CrashLanding、
    /// 疏浚 Dredge、尼奥的怒火 NeowsFury）：OnPlay 补牌空间计算需排除英雄技能卡。
    /// 游戏版本更新导致状态机命名/结构变化时 TargetMethods 会静默失配——
    /// 由 <see cref="VerifyTargets"/> 在启动时显式告警。
    /// </summary>
    private static readonly Type[] HeroPowerAffectedVanillaCards =
    [
        typeof(MegaCrit.Sts2.Core.Models.Cards.Scrawl),
        typeof(MegaCrit.Sts2.Core.Models.Cards.Anointed),
        typeof(MegaCrit.Sts2.Core.Models.Cards.CrashLanding),
        typeof(MegaCrit.Sts2.Core.Models.Cards.Dredge),
        typeof(MegaCrit.Sts2.Core.Models.Cards.NeowsFury)
    ];

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var type in HeroPowerAffectedVanillaCards)
        {
            foreach (var nested in type.GetNestedTypes(BindingFlags.NonPublic))
            {
                if (nested.Name.StartsWith("<OnPlay>d__", System.StringComparison.Ordinal))
                {
                    var moveNext = nested.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (moveNext != null)
                    {
                        yield return moveNext;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 启动时验证（Entry.Init 在 PatchAll 之后调用）：5 张原版卡的 OnPlay 状态机
    /// 是否全部可定位。游戏版本更新导致状态机命名/结构变化时，TargetMethods 会
    /// <b>静默失配</b>（patch 不生效、不崩溃、无报错）——本验证把静默失效变成
    /// 显式 Warn 告警：启动日志出现即表示该卡需更新进 <see cref="HeroPowerAffectedVanillaCards"/>。
    /// </summary>
    public static void VerifyTargets()
    {
        foreach (var type in HeroPowerAffectedVanillaCards)
        {
            bool found = false;
            foreach (var nested in type.GetNestedTypes(BindingFlags.NonPublic))
            {
                if (nested.Name.StartsWith("<OnPlay>d__", System.StringComparison.Ordinal) &&
                    nested.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic) != null)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                MegaCrit.Sts2.Core.Logging.Log.Warn(
                    $"[Jaina] HeroPowerHandFullDrawCardPatch: {type.Name} 的 <OnPlay>d__ 状态机未找到，" +
                    "英雄技能卡不占手牌位 patch 对该卡失效——游戏版本可能已更新，请更新 TargetMethods 卡列表");
            }
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        for (int i = 0; i < codes.Count; i++)
        {
            // 模式 A：get_Cards + get_Count（前一条为产生 CardPile 的指令，保留）
            if (codes[i].opcode == OpCodes.Callvirt && Equals(codes[i].operand, GetCards) &&
                i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Callvirt && Equals(codes[i + 1].operand, GetCount))
            {
                codes.RemoveRange(i, 2);
                codes.InsertRange(i, new[]
                {
                    new CodeInstruction(OpCodes.Call, NonHeroCountFromPile)
                });
            }
            // 模式 B：GetCards(owner, Hand) 后跟 Enumerable.Count
            else if (codes[i].opcode == OpCodes.Call && Equals(codes[i].operand, EnumerableCount) &&
                     i - 1 >= 0 && codes[i - 1].opcode == OpCodes.Call && Equals(codes[i - 1].operand, CardPileGetCards))
            {
                codes[i] = new CodeInstruction(OpCodes.Call, NonHeroCountFromEnumerable);
            }
        }
        return codes;
    }
}
