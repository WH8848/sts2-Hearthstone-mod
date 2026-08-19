using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Powers;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 沙坑（SandpitPower，贪婪之王/沙虫吞噬）防御：
/// 原版 AfterRemoved 在"沙坑目标相关玩家已死亡"时仍执行吞噬流程——
/// 对已死玩家/随从 GetCreatureNode 返回 null 后解引用 → NullReferenceException
/// → 回合循环（turn loop）崩溃 → 战斗卡死（实测：沙虫沙死玩家后卡住）。
/// 目标玩家已死/无效时吞噬无意义（玩家已死），直接跳过原逻辑。
/// </summary>
[HarmonyPatch(typeof(SandpitPower), "AfterRemoved")]
public static class SandpitAfterRemovedFix
{
    public static bool Prefix(SandpitPower __instance)
    {
        try
        {
            var target = __instance.Target;
            if (target == null || target.Player == null || target.Player.Creature == null ||
                target.Player.Creature.IsDead)
            {
                // 目标或其主人的玩家已死/无效：跳过吞噬（原逻辑会 NRE 崩回合循环）
                return false;
            }
            return true;
        }
        catch
        {
            return true;
        }
    }
}
