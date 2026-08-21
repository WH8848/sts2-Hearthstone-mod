using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Commands;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 动画触发安全兜底（两层）：
/// 1. <see cref="AnimStateCallTriggerSafePatch"/>：<see cref="AnimState.CallTrigger"/>
///    的动画条件 lambda 异常不中断（Transpiler 把 condition() 调用替换为 try-catch
///    包装——<b>所有</b>动画触发路径最终都汇聚到这里，含 CreatureCmd.TriggerAnim、
///    DoomPower 直接 SetAnimationTrigger、NCreature 内部 Dead/Revive 触发）。
/// 2. <see cref="TriggerAnimTaskSafePatch"/>：<see cref="CreatureCmd.TriggerAnim"/>
///    的 Task 再包一层 try-catch（双保险，模式同 CardStuckInPlayAfterPlayPatch）。
///
/// 根因（实测日志）：RitsuLib 的 AttackHitHook 在<b>伤害结算之后</b>触发受击动画
/// （原版时序是先播动画后结算伤害）。带"一次性/可移除 Power"的怪物动画条件
/// lambda 在 Power 被移除后 NRE（GetPower 返回 null 后解引用）：
/// - 寄生惧魔 Parafright：<c>() =&gt; !GetPower&lt;IllusionPower&gt;().IsReviving</c>
///   （1 层幻象，第一次被攻击后移除）；
/// - 幻象园丁 PhantasmalGardener：<c>GetPower&lt;SkittishPower&gt;().HasGainedBlockThisTurn</c>。
/// 动画是纯视觉层，失败不应中断伤害/法术结算（火焰风暴 7 次伤害第 1 次即崩、
/// 大法师的符文后续法术全部中断，都是该 NRE 冒泡所致）。
/// </summary>
public static class TriggerAnimSafePatch
{
    /// <summary>
    /// 安全调用动画条件：异常（怪物 Power 已移除等）返回 false（不切换动画），
    /// 不影响任何游戏逻辑。
    /// </summary>
    private static bool SafeCondition(Func<bool>? condition)
    {
        if (condition == null)
        {
            return true;
        }
        try
        {
            return condition();
        }
        catch (Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] Anim condition failed (visual only): {ex.GetType().Name} {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// AnimState.CallTrigger：把 <c>condition.Invoke()</c> 替换为
    /// <see cref="SafeCondition"/>（try-catch 包装）。覆盖所有动画触发路径。
    /// </summary>
    [HarmonyPatch(typeof(AnimState), nameof(AnimState.CallTrigger))]
    public static class AnimStateCallTriggerSafePatch
    {
        private static readonly MethodInfo InvokeMethod =
            typeof(Func<bool>).GetMethod("Invoke")!;

        private static readonly MethodInfo SafeConditionMethod =
            AccessTools.Method(typeof(TriggerAnimSafePatch), nameof(SafeCondition))!;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var code in instructions)
            {
                // condition() → SafeCondition(condition)：调用点栈上已有 Func<bool>，
                // 替换 callvirt Invoke 为 call SafeCondition（签名匹配，参数即栈上的 Func）
                if (code.opcode == OpCodes.Callvirt && Equals(code.operand, InvokeMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, SafeConditionMethod);
                }
                else
                {
                    yield return code;
                }
            }
        }
    }

    /// <summary>
    /// CreatureCmd.TriggerAnim 的 Task 再包一层 try-catch（双保险）。
    /// </summary>
    [HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim))]
    public static class TriggerAnimTaskSafePatch
    {
        public static void Postfix(ref Task __result)
        {
            __result = WrapAsync(__result);
        }

        private static async Task WrapAsync(Task original)
        {
            try
            {
                await original;
            }
            catch (Exception ex)
            {
                // 动画失败（如怪物幻象消失后动画条件 NRE）仅影响视觉，不影响游戏逻辑
                MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] TriggerAnim animation failed (visual only): {ex.GetType().Name} {ex.Message}");
            }
        }
    }
}
