using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace jaina.Scripts.Character.Relics;

/// <summary>
/// 欧罗巴斯之触兼容补丁：把吉安娜的初始遗物"旗鼓相当的对手"纳入
/// 先古升级映射（否则 TouchOfOrobas.GetUpgradedStarterRelic 找不到映射，
/// 会退化成"花环" Circlet）。
/// 升级目标：正在撬动对手的回合结束按钮（每场战斗开始获得幸运币+抽 1 张牌）。
/// </summary>
[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
public static class TouchOfOrobasPatch
{
    private static void Postfix(RelicModel starterRelic, ref RelicModel __result)
    {
        if (starterRelic is EvenMatch)
        {
            __result = ModelDb.Relic<EvenMatchAncient>();
        }
    }
}
