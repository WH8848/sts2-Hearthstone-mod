using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 动态 CardType.Minion 的兼容补丁。
/// 游戏的 CardType switch 对未知值会抛 ArgumentOutOfRangeException，需要把动态值
/// 映射到 Skill 的表现（卡框/边框/先古背景），类型标签文本由 ToLocString patch 提供。
/// </summary>
public static class JainaCardTypePatches
{
    /// <summary>
    /// 动态"随从"类型标签显示：CARD_TYPE.MINION 本地化文本
    /// </summary>
    [HarmonyPatch(typeof(CardTypeExtensions), "ToLocString")]
    private static class MinionLocStringPatch
    {
        private static bool Prefix(CardType cardType, ref LocString __result)
        {
            if (cardType == JainaCardTypes.Minion)
            {
                __result = new LocString("gameplay_ui", "CARD_TYPE.MINION");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 卡框材质：动态随从类型映射为技能卡框
    /// </summary>
    [HarmonyPatch(typeof(CardModel), "FramePath", MethodType.Getter)]
    private static class MinionFramePathPatch
    {
        private static void Postfix(CardModel __instance, ref string __result)
        {
            if (__instance.Type == JainaCardTypes.Minion)
            {
                __result = ImageHelper.GetImagePath("atlases/ui_atlas.sprites/card/card_frame_skill_s.tres");
            }
        }
    }

    /// <summary>
    /// 卡牌边框：动态随从类型映射为技能边框
    /// </summary>
    [HarmonyPatch(typeof(CardModel), "PortraitBorderPath", MethodType.Getter)]
    private static class MinionPortraitBorderPathPatch
    {
        private static void Postfix(CardModel __instance, ref string __result)
        {
            if (__instance.Type == JainaCardTypes.Minion)
            {
                __result = ImageHelper.GetImagePath("atlases/ui_atlas.sprites/card/card_portrait_border_skill_s.tres");
            }
        }
    }

    /// <summary>
    /// 先古卡文本背景：动态随从类型映射为技能（仅先古卡访问，保险起见）
    /// </summary>
    [HarmonyPatch(typeof(CardModel), "AncientTextBgPath", MethodType.Getter)]
    private static class MinionAncientTextBgPathPatch
    {
        private static void Postfix(CardModel __instance, ref string __result)
        {
            if (__instance.Type == JainaCardTypes.Minion)
            {
                __result = ImageHelper.GetImagePath("atlases/compressed_atlas.sprites/ancient_text_bg_skill.png.tres");
            }
        }
    }
}
