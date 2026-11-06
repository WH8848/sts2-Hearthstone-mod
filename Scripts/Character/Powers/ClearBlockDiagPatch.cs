using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 防御补丁：回合开始清格挡（ClearBlock → ShouldClearBlock → IterateCombatHookListeners）
/// 对"已死亡但残留、CombatState 已被置 null"的随从抛 NullReferenceException，
/// 异常会打死 turn loop、战斗永久卡死（实测 AegwynnMinion）。
/// 这里在 CombatState 为 null 时直接跳过 ClearBlock（Prefix 返回 false），仅保留一行 Warn。
/// 注意：残留尸体本身已由 JainaMinionBase.AfterDeath 强制摘除 + JainaPetResidueCleanupPower
/// 兜底清理；本补丁只是最后一道防卡死保险。
/// </summary>
public static class ClearBlockDiagPatch
{
    private static int _skipLogCount;

    [HarmonyPatch(typeof(Creature), "ClearBlock")]
    public static class ClearBlockPrefix
    {
        private static bool Prefix(Creature __instance)
        {
            if (__instance.CombatState != null)
            {
                return true;
            }
            // 日志限流：最多 5 条，避免残留较多时刷屏
            if (System.Threading.Interlocked.Increment(ref _skipLogCount) <= 5)
            {
                MegaCrit.Sts2.Core.Logging.Log.Warn(
                    $"[Jaina] skip ClearBlock on detached creature {__instance.Name} " +
                    $"(side={__instance.Side} pet={__instance.IsPet} alive={__instance.IsAlive})");
            }
            return false;
        }
    }
}
