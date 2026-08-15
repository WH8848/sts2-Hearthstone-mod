using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.addons.mega_text;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 费用升级方向追踪：记录 CardEnergyCost.UpgradeBy 的正负号，
/// 供 NCard.UpdateEnergyCostColor patch 使用——升级预览中
/// 减费（UpgradeBy 负数）标绿、加费（UpgradeBy 正数）标蓝
/// （与原版手牌"费用增加"的 energyBlue 颜色一致）。
/// 弱引用随卡实例回收自动清理。
/// </summary>
public static class UpgradeSignTracker
{
    private sealed class SignBox
    {
        public int Sign;
    }

    private static readonly ConditionalWeakTable<CardEnergyCost, SignBox> Signs = new();

    public static void Record(CardEnergyCost cost, int addend)
    {
        Signs.Remove(cost);
        Signs.Add(cost, new SignBox { Sign = addend });
    }

    public static bool TryGetSign(CardEnergyCost cost, out int sign)
    {
        if (Signs.TryGetValue(cost, out var box))
        {
            sign = box.Sign;
            return true;
        }
        sign = 0;
        return false;
    }
}

/// <summary>
/// 记录 UpgradeBy 的符号（升级加费/减费方向）
/// </summary>
[HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.UpgradeBy))]
public static class UpgradeBySignPatch
{
    private static void Postfix(CardEnergyCost __instance, int addend)
    {
        UpgradeSignTracker.Record(__instance, addend);
    }
}

/// <summary>
/// 升级预览费用颜色：原版 WasJustUpgraded 一律绿色；
/// 按用户规则改为：UpgradeBy 负数（减费）绿色、正数（加费）蓝色
/// （与原版手牌费用增加 energyBlue 一致）。
/// </summary>
[HarmonyPatch(typeof(NCard), "UpdateEnergyCostColor")]
public static class EnergyCostColorPatch
{
    private static readonly AccessTools.FieldRef<NCard, MegaLabel> EnergyLabelRef =
        AccessTools.FieldRefAccess<NCard, MegaLabel>("_energyLabel");

    private static void Postfix(NCard __instance)
    {
        var cost = __instance.Model?.EnergyCost;
        if (cost == null || cost.CostsX || !cost.WasJustUpgraded)
        {
            return;
        }
        if (!UpgradeSignTracker.TryGetSign(cost, out var sign) || sign <= 0)
        {
            // 无记录或减费：保持原版绿色
            return;
        }
        // 加费升级：费用标蓝（与原版手牌"费用增加"同色）
        EnergyLabelRef(__instance).AddThemeColorOverride(ThemeConstants.Label.FontColor, StsColors.energyBlue);
        EnergyLabelRef(__instance).AddThemeColorOverride(ThemeConstants.Label.FontOutlineColor, StsColors.energyBlueOutline);
    }
}
