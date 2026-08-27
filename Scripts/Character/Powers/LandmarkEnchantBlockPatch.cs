using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using jaina.Scripts.Character.Cards;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 地标不可被附魔（炉石规则:地标只能点击使用,不能被其它任何方式"选择"——附魔、
/// 克隆营地/佩尔的成长等所有附魔来源最终都走 EnchantmentModel.CanEnchant,
/// Cardinal 附魔调用方先过滤候选;此 Prefix 对地标统一返回 false。
/// 其余"选择"路径已核实安全：地标非法术/随从类型→不进发现法术/随从池、
/// 不进随机释放池(攻击/技能/能力)、幻觉药水/模拟幻影已显式排除地标。
/// <b>另外</b>：Clone(转刻/佩尔的成长)附魔候选排除<b>英雄技能卡</b>
/// （英雄技能不可被复制——原实现用 PaelsGrowth.AfterObtained 的随机选卡
/// 过滤,已删除;此处 CanEnchant 层统一拦截,其余候选交给原版玩家自选流程）。
/// 注意：CanAfflict 声明于基类——__instance is Tainted 式限定（同类 Harmony 教训）。
/// </summary>
public static class LandmarkEnchantBlockPatch
{
    [HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.CanEnchant))]
    public static class CanEnchantPrefix
    {
        private static bool Prefix(EnchantmentModel __instance, CardModel card, ref bool __result)
        {
            if (card is JainaLandmarkCardTemplate)
            {
                __result = false;
                return false;
            }
            if (__instance is MegaCrit.Sts2.Core.Models.Enchantments.Clone &&
                card != null &&
                jaina.Scripts.Character.Powers.HeroPowerHandHelper.IsHeroPowerCard(card))
            {
                // 克隆附魔不能施加/选择英雄技能卡（英雄技能不可被复制）
                __result = false;
                return false;
            }
            return true;
        }
    }
}
