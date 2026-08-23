using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 地标不可被卡牌选择（炉石规则：地标只能被点击使用,不能成为任何卡牌的目标——
/// 火球术/寒冰箭/战吼目标/随机释放池等所有经 CardModel.IsValidTarget 判定的选择）。
/// 全局统一出口：IsValidTarget 对地标单位一律返回 false——
/// 覆盖自定义目标类型(AnyTargetable 等)、原版 AnyEnemy 类与随机释放/选择的合法目标池。
/// </summary>
public static class LandmarkUnselectablePatch
{
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
    public static class IsValidTargetPrefix
    {
        private static bool Prefix(CardModel __instance, Creature? target, ref bool __result)
        {
            if (target?.Monster is JainaLandmarkBase)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
