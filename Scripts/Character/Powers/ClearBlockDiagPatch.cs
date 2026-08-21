using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 诊断补丁（临时）：回合开始清格挡时（ClearBlock → ShouldClearBlock →
/// IterateCombatHookListeners）出现 NullReferenceException 导致 turn loop 卡死。
/// 该 NRE 的原始异常/损坏项无法从栈中看清，本 Prefix 在原逻辑执行前
/// 主动遍历一遍 hook listeners，捕获并打印：
/// 1) 列表中的 null 项（IterateCombatHookListeners 放行了 null 或者
///    CombatState.IterateHookListeners 某条 Add 了 null）；
/// 2) 遍历过程自身抛出的完整异常（含 inner/stack）。
/// 定位后即可移除本补丁。
/// </summary>
public static class ClearBlockDiagPatch
{
    [HarmonyPatch(typeof(Creature), "ClearBlock")]
    public static class ClearBlockPrefix
    {
        private static void Prefix(Creature __instance)
        {
            var state = __instance.CombatState;
            if (state == null)
            {
                return;
            }
            try
            {
                var listeners = state.IterateHookListeners().ToList();
                int idx = 0;
                foreach (var m in listeners)
                {
                    if (m == null)
                    {
                        MegaCrit.Sts2.Core.Logging.Log.Error(
                            $"[JainaDiag] ClearBlock hook listener NULL at index {idx}/{listeners.Count} " +
                            $"caller={__instance.Name} side={__instance.Side} alive={__instance.IsAlive}");
                    }
                    idx++;
                }
                MegaCrit.Sts2.Core.Logging.Log.Info(
                    $"[JainaDiag] ClearBlock listeners OK: {listeners.Count} items for {__instance.Name} side={__instance.Side}");
            }
            catch (Exception ex)
            {
                MegaCrit.Sts2.Core.Logging.Log.Error($"[JainaDiag] ClearBlock iterate THREW: {ex}");
            }
        }
    }
}
