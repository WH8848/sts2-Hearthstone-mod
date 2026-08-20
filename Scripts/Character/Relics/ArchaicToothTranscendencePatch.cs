using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Relics;
using jaina.Scripts.Character.Cards;

namespace jaina.Scripts.Character.Relics;

/// <summary>
/// 修复：英雄技能卡（火焰冲击）的 Eternal 保护与"古老牙齿"（Archaic Tooth）
/// 遗物超越机制冲突——游戏卡死。
///
/// 冲突链：
/// 1. 火焰冲击 CanonicalKeywords 含 Eternal（防营地移除/PaelsTooth/WoodCarvings 等
///    永久删出牌库的保护，用户要求）；
/// 2. 古老牙齿 AfterObtained → CardCmd.Transform(火焰冲击, 二级火焰冲击)；
/// 3. CardTransformation 构造函数 AssertTransformable 检查 IsTransformable——
///    带 Eternal 的卡在牌库中 IsTransformable=false（IsRemovable=false 派生），
///    抛 InvalidOperationException("Non-removable cards cannot be transformed!")，
///    遗物获取流程中断 → 游戏无法继续。
///
/// 修复：Prefix 在变换前只对"将被超越的那张火焰冲击"临时移除 Eternal。
/// 变换成功后原卡实例被替换/废弃，新卡（二级火焰冲击）由 canonical 创建，
/// 其 CanonicalKeywords 自带 Eternal → 无需恢复；其他英雄技能卡/其他路径
/// 的移除、变形保护完全不受影响。
/// </summary>
[HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.AfterObtained))]
public static class ArchaicToothTranscendencePatch
{
    private static void Prefix(ArchaicTooth __instance)
    {
        try
        {
            var player = __instance.Owner;
            if (player?.Deck == null)
            {
                return;
            }
            // 与 ArchaicTooth.GetTranscendenceStarterCard 的匹配语义一致：
            // 取牌库中第一张将被超越的火焰冲击（含升级形态，类型不变）。
            var starter = player.Deck.Cards.FirstOrDefault(c => c is Fireblast);
            if (starter == null || !starter.IsMutable)
            {
                return;
            }
            if (starter.Keywords.Contains(MegaCrit.Sts2.Core.Entities.Cards.CardKeyword.Eternal))
            {
                starter.RemoveKeyword(MegaCrit.Sts2.Core.Entities.Cards.CardKeyword.Eternal);
            }
        }
        catch
        {
            // 移除失败不影响遗物主流程（原版逻辑继续，异常只会在无 Eternal 卡时不再触发）
        }
    }
}
