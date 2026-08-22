using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 诊断补丁（临时）：回合开始清格挡（ClearBlock → ShouldClearBlock →
/// IterateCombatHookListeners）出现 NullReferenceException 导致 turn loop 卡死，
/// 且仅在"某个 creature 的 CombatState == null 时"复现（上一版诊断在 CombatState
/// null 时直接 return 未打印——正是漏掉的那个 creature）。
/// 本版：无论 CombatState 是否 null 都打印 creature 身份；再遍历一遍 hook listeners
/// 打印 null 项与完整异常。定位后移除。
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
                MegaCrit.Sts2.Core.Logging.Log.Error(
                    $"[JainaDiag] ClearBlock on {__instance.Name} side={__instance.Side} " +
                    $"isPet={__instance.IsPet} isPlayer={__instance.IsPlayer} " +
                    $"monster={__instance.Monster?.GetType().Name ?? "null"} alive={__instance.IsAlive} " +
                    $"-- CombatState IS NULL (this creature is the NRE source!)");
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
                            $"[JainaDiag] ClearBlock hook listener NULL at {idx}/{listeners.Count} " +
                            $"caller={__instance.Name} side={__instance.Side}");
                    }
                    idx++;
                }
            }
            catch (Exception ex)
            {
                MegaCrit.Sts2.Core.Logging.Log.Error($"[JainaDiag] ClearBlock iterate THREW: {ex}");
            }
        }
    }
}
