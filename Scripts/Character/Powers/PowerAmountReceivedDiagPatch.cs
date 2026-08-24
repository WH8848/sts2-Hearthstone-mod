using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 施加数量修正诊断：Hook.ModifyPowerAmountReceived 前后记录。
/// 定位"冻结施加被清零(amount=0)"与"柔嫩的 -1 力量/-1 敏捷不一致"——
/// 每次 Power 施加进入修正链时记录原始数量与目标，
/// 修正完成后输出实际生效数量与<b>修改器清单</b>（可以精确点名清零者）。
/// 只记录冻结/力量/敏捷（避免刷屏）。
/// </summary>
public static class PowerAmountReceivedDiagPatch
{
    private static readonly IReadOnlySet<string> Tracked = new HashSet<string>
    {
        "JAINA_POWER_FREEZE_POWER",
        "STRENGTH_POWER",
        "DEXTERITY_POWER",
    };

    private static bool IsTracked(PowerModel power)
    {
        return power != null && Tracked.Contains(power.Id.Entry);
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.ModifyPowerAmountReceived))]
    private static class ModifyPowerAmountReceivedDiag
    {
        private static void Prefix(PowerModel canonicalPower, Creature target, decimal amount, Creature? giver)
        {
            if (!IsTracked(canonicalPower) || amount == 0m)
            {
                return;
            }
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[JainaPowerMod] pre target={target?.Name}(side={target?.Side}) {canonicalPower.Id.Entry} amount={amount} giver={(giver == null ? "null" : giver.Name)}");
        }

        private static void Postfix(PowerModel canonicalPower, Creature target, decimal amount, Creature? giver,
            ref IEnumerable<AbstractModel> modifiers)
        {
            if (!IsTracked(canonicalPower))
            {
                return;
            }
            var mods = modifiers ?? [];
            var names = string.Join(",", mods.Select(m => m.GetType().Name));
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[JainaPowerMod] post target={target?.Name} {canonicalPower.Id.Entry} amount={amount} giver={(giver == null ? "null" : giver.Name)} modifiers=[{names}]");
        }
    }
}
