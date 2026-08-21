using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 动画触发安全兜底：<see cref="CreatureCmd.TriggerAnim"/> 的动画回调异常不中断游戏逻辑。
///
/// 根因（实测日志）：RitsuLib 的 AttackHitHook 在<b>伤害结算之后</b>触发受击动画
/// （原版时序是先播动画后结算伤害）。寄生惧魔（Parafright）只有 1 层幻象
/// （IllusionPower），第一次被攻击后幻象被移除，此后任何 "Hit" 动画触发时其
/// 动画条件 lambda <c>() =&gt; !base.Creature.GetPower&lt;IllusionPower&gt;().IsReviving</c>
/// 中 GetPower 返回 null → NullReferenceException → 冒泡到卡牌 OnPlay：
/// - 火焰风暴（7 次 5 点伤害）：第 1 次伤害动画即崩 → 只剩 1 次伤害；
/// - 大法师的符文：随机施放的攻击牌动画崩 → 后续法术全部中断。
///
/// 修复：把 TriggerAnim 的 Task 包一层 try-catch——动画是纯视觉层，
/// 失败不影响伤害/法术结算（Postfix 替换 __result，模式同
/// CardStuckInPlayAfterPlayPatch.WrapAsync）。
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
        catch (System.Exception ex)
        {
            // 动画失败（如怪物幻象消失后动画条件 NRE）仅影响视觉，不影响游戏逻辑
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] TriggerAnim animation failed (visual only): {ex.GetType().Name} {ex.Message}");
        }
    }
}
