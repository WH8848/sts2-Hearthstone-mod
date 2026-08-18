using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 冻结无视人工制品（ArtifactPower）阻挡。
/// 原版 ArtifactPower.TryModifyPowerAmountReceived 会把施加到目标身上的可见 Debuff
/// 层数改为 0 并消耗 1 层人工制品（"人工制品抵挡负面效果"机制）。
/// 滑冰元素/瓦尔登·晨拥的战吼冻结按炉石规则**不被人工制品阻挡**
/// （施加前由 <see cref="FreezePower.BypassArtifactNextApply"/> 置位，见两随从的战吼实现）；
/// 其余来源的冻结（寒冰箭/冰霜新星等）保持原版行为（会被人工制品抵挡）。
/// 联机：置位/清除在两端战吼中确定性执行，patch 求值一致。
/// </summary>
public static class ArtifactFreezeBypassPatch
{
    [HarmonyPatch(typeof(ArtifactPower), "TryModifyPowerAmountReceived")]
    private static class PrefixPatch
    {
        private static bool Prefix(PowerModel canonicalPower, Creature target, decimal amount, Creature? _,
            ref decimal modifiedAmount, ref bool __result)
        {
            if (canonicalPower is FreezePower && FreezePower.BypassArtifactNextApply)
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
