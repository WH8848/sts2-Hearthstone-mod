using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using jaina.Scripts.Character.Cards;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 污染（Tainted）不作用于幸运币。
/// 感染棱镜的 VitalSparkPower 会把所有技能牌染上"污染"（每层污染打出时
/// 给持有者上 TaintedPower 结算伤害），且随 VitalSparkPower 叠层重染——
/// 幸运币（0费白给的硬币衍生物，Skill 类型）会被多个棱镜来源反复叠层
/// （如 2 人局两个棱镜各 2 层 + 生成入库后再染，实测第一回合就 6 层）。
/// 幸运币不应被染上污染：其余技能牌保持原版行为（会被污染）。
/// 判定走 Tainted.CanAfflict 入口（CardCmd.Afflict 统一调用），前缀拦截即可。
/// 注意：Tainted 未覆写 CanAfflict，该方法声明于 AfflictionModel——
/// 本 patch 会作用到所有灾厄的 CanAfflict，因此用 __instance is Tainted 限定只拦污染。
/// </summary>
public static class TaintedExcludeLuckyCoinPatch
{
    [HarmonyPatch(typeof(Tainted), nameof(Tainted.CanAfflict))]
    private static class TaintedCanAfflictPrefix
    {
        private static bool Prefix(AfflictionModel __instance, CardModel card, ref bool __result)
        {
            if (__instance is Tainted && card is LuckyCoin)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
