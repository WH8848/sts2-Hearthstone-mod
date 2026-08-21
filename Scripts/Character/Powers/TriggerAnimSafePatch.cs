using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 动画触发安全兜底：<see cref="CreatureCmd.TriggerAnim"/> 的 Task 包一层 try-catch，
/// 动画回调异常（怪物动画条件 lambda NRE）不中断伤害/法术结算。
///
/// 根因（实测日志）：RitsuLib 的 AttackHitHook 在<b>伤害结算之后</b>触发受击动画
/// （原版时序是先播动画后结算伤害）。带"一次性/可移除 Power"的怪物动画条件
/// lambda 在 Power 被移除后 NRE（GetPower 返回 null 后解引用）：
/// - 寄生惧魔 Parafright：<c>() =&gt; !GetPower&lt;IllusionPower&gt;().IsReviving</c>
///   （1 层幻象，第一次被攻击后移除）；
/// - 幻象园丁 PhantasmalGardener：<c>GetPower&lt;SkittishPower&gt;().HasGainedBlockThisTurn</c>。
/// 动画是纯视觉层，失败不应中断游戏逻辑（火焰风暴 7 次伤害第 1 次即崩、
/// 大法师的符文后续法术全部中断，都是该 NRE 冒泡所致）。
///
/// <b>实现说明</b>：只用 Task 层 try-catch（Postfix 替换 __result，模式同
/// CardStuckInPlayAfterPlayPatch.WrapAsync）——<b>不用</b> AnimState.CallTrigger 的
/// Transpiler（会触发 Harmony "Bad label content in ILGenerator"，dll 初始化失败）
/// 也<b>不用</b>反射 Prefix 手动调用原方法（动画高频路径 + 可空返回类型反射有
/// 崩溃隐患——实测打出二级火焰冲击后游戏卡死崩溃）。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim))]
public static class TriggerAnimSafePatch
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
