using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using jaina.Scripts.Character.Keywords;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 旅社谍战洗入卡牌"全费用系统归零"补丁。
/// 洗入时能量费用（SetCustomBaseCost 0）与星星费用（BaseStarCost 0）已置零；
/// 但 <b>X 费用卡</b>（能量 X / 星星 X）打出时的花费 = 当前能量/星星，不走 _base：
/// - CardEnergyCost.GetAmountToSpend：能量 X 卡返回玩家当前能量
/// - CardModel.GetStarCostWithModifiers：星星 X 卡返回玩家当前星星
/// 这里对带 ZeroCostMark 标记（旅社谍战洗入）的 X 费卡强制 0 花费
/// （同时 CapturedXValue=0 → 效果 X 值也为 0），并让卡面费用显示 0。
/// 普通能量/星星卡的费用显示走 _base（已为 0），无需处理。
/// 联机：标记在 Keywords 集合中序列化，两端求值一致。
/// </summary>
public static class ZeroCostMarkPatch
{
    /// <summary>
    /// 该卡是否旅社谍战洗入的零费标记卡
    /// </summary>
    private static bool IsZeroCostMarked(CardModel card)
    {
        return card != null && card.Keywords.Contains(JainaKeywords.ZeroCostMark);
    }

    /// <summary>
    /// 能量 X 费卡：打出花费强制 0（原逻辑 = 玩家当前能量）
    /// </summary>
    [HarmonyPatch(typeof(CardEnergyCost), "GetAmountToSpend")]
    private static class EnergyXSpendPrefix
    {
        private static bool Prefix(CardEnergyCost __instance, ref int __result)
        {
            if (!__instance.CostsX)
            {
                return true;
            }
            // CardEnergyCost._card 为私有字段，反射读取（Harmony AccessTools 可访问）
            var card = AccessTools.Field(typeof(CardEnergyCost), "_card").GetValue(__instance) as CardModel;
            if (IsZeroCostMarked(card))
            {
                __result = 0;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 能量费用（含本地/全局修改器）：带标记的牌强制 0。
    /// 覆盖其他 mod 通过 Hook.ModifyEnergyCostInCombat 等全局费用修改实现的自定义能量系统——
    /// 基础费归零后 Hook 仍会在其上叠加费用，这里把费用计算整体锁 0。
    /// 同时保证 CanPlay 检查（GetWithModifiers &gt; 0）与卡面显示一致为 0。
    /// </summary>
    [HarmonyPatch(typeof(CardEnergyCost), "GetWithModifiers")]
    private static class EnergyCostLockPrefix
    {
        private static bool Prefix(CardEnergyCost __instance, ref int __result)
        {
            var card = AccessTools.Field(typeof(CardEnergyCost), "_card").GetValue(__instance) as CardModel;
            if (card != null && IsZeroCostMarked(card))
            {
                __result = 0;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 星星费用（含全局修改器）：带标记的牌强制 0。
    /// 覆盖其他 mod 通过 Hook.ModifyStarCost 等全局星星费用修改实现的自定义能量系统；
    /// 普通星星卡（BaseStarCost 归零后 Hook 可能再加）与星星 X 卡（原逻辑 = 当前星星）一并锁 0。
    /// </summary>
    [HarmonyPatch(typeof(CardModel), "GetStarCostWithModifiers")]
    private static class StarXSpendPrefix
    {
        private static bool Prefix(CardModel __instance, ref int __result)
        {
            if (IsZeroCostMarked(__instance))
            {
                __result = 0;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 能量 X 费卡卡面费用显示：旅社谍战洗入的牌显示 0（原逻辑显示 X）
    /// </summary>
    [HarmonyPatch(typeof(NCard), "UpdateEnergyCostVisuals")]
    private static class EnergyXVisualsPostfix
    {
        private static void Postfix(NCard __instance)
        {
            try
            {
                var model = __instance.Model;
                if (model == null || !model.EnergyCost.CostsX || !IsZeroCostMarked(model))
                {
                    return;
                }
                var label = __instance.GetNode<MegaLabel>("%EnergyLabel");
                if (label != null)
                {
                    label.SetTextAutoSize("0");
                }
            }
            catch
            {
                // 展示层补丁：异常不影响原版显示
            }
        }
    }

    /// <summary>
    /// 星费用卡面显示：旅社谍战洗入的牌——
    /// <b>费星卡（原本有星费用或星星 X）显示"0"星（0费0星）</b>，
    /// 无星卡保持隐藏（原版逻辑 BaseStarCost=-1 不显示）。
    /// 覆盖原版 `GetStarCostWithModifiers() &gt;= 0` 对归零后费星卡显示"0"的行为（这正是需要的），
    /// 同时避免星 X 卡显示 X。
    /// </summary>
    [HarmonyPatch(typeof(NCard), "UpdateStarCostVisuals")]
    private static class StarVisualsPostfix
    {
        private static void Postfix(NCard __instance)
        {
            try
            {
                var model = __instance.Model;
                if (model == null || !IsZeroCostMarked(model))
                {
                    return;
                }
                var icon = __instance.GetNode<Godot.Control>("%StarIcon");
                var label = __instance.GetNode<MegaLabel>("%StarLabel");
                // 费星卡（基础星费用 >= 0 或星星 X）→ 显示 0 星；无星卡 → 隐藏
                bool hasStarCost = model.HasStarCostX || model.BaseStarCost >= 0;
                if (label != null)
                {
                    label.SetTextAutoSize(hasStarCost ? "0" : string.Empty);
                }
                if (icon != null)
                {
                    icon.Visible = hasStarCost;
                }
            }
            catch
            {
                // 展示层补丁：异常不影响原版显示
            }
        }
    }

    /// <summary>
    /// 星费用文字更新入口（费用变化时）：同样按费星卡显示 0 星 / 无星卡隐藏
    /// </summary>
    [HarmonyPatch(typeof(NCard), "UpdateStarCostText")]
    private static class StarTextPostfix
    {
        private static void Postfix(NCard __instance)
        {
            try
            {
                var model = __instance.Model;
                if (model == null || !IsZeroCostMarked(model))
                {
                    return;
                }
                var icon = __instance.GetNode<Godot.Control>("%StarIcon");
                var label = __instance.GetNode<MegaLabel>("%StarLabel");
                bool hasStarCost = model.HasStarCostX || model.BaseStarCost >= 0;
                if (label != null)
                {
                    label.SetTextAutoSize(hasStarCost ? "0" : string.Empty);
                }
                if (icon != null)
                {
                    icon.Visible = hasStarCost;
                }
            }
            catch
            {
                // 展示层补丁：异常不影响原版显示
            }
        }
    }
}
