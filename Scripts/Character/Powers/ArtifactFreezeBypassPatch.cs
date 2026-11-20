using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 冻结无视人工制品（ArtifactPower）阻挡。
/// 原版 ArtifactPower.TryModifyPowerAmountReceived 会把施加到目标身上的可见 Debuff
/// 层数改为 0 并消耗 1 层人工制品（"人工制品抵挡负面效果"机制）。
/// 随从效果通过 <see cref="FreezePower.ApplyIgnoringArtifact"/> 施加冻结。
/// </summary>
public static class ArtifactFreezeBypassPatch
{
    [HarmonyPatch(typeof(ArtifactPower), "TryModifyPowerAmountReceived")]
    private static class PrefixPatch
    {
        private static bool Prefix(PowerModel canonicalPower, Creature target, decimal amount,
            ref decimal modifiedAmount, ref bool __result)
        {
            if (canonicalPower is FreezePower && FreezePower.BypassesArtifact(target))
            {
                // 不修改施加层数（跳过人工制品抵挡），原方法不再执行
                modifiedAmount = amount;
                __result = false;
                return false;
            }
            return true;
        }
    }
}
