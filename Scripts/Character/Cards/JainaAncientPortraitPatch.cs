using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 先古卡样式开启时，把所有引用 assets/card_art/ 的卡图换成 assets/card_art_ancient/ 竖版
/// （250x351 竖版）。
/// 证据：CardModel.PortraitPath 是 public virtual string（0.113 src/Core/Models/CardModel.cs:165），
/// CardModel.Portrait = ResourceLoader.Load&lt;Texture2D&gt;(PortraitPath)（同文件 179）；
/// RitsuLib 用 CustomPortraitPath 覆盖 __result（ContentAssetModelVisualOverridePatches.cs:153/216），
/// 所以这里用 Priority.Last 在其之后再改一次。
/// 不按卡池过滤：任务奖励 Token、多人卡等也走 assets/card_art/，需要一起覆盖
/// （JainaCardStyle.ToAncientPath 只处理 /assets/card_art/ 路径，不会影响其他 mod 的卡图目录）。
/// </summary>
[HarmonyPatch(typeof(CardModel), "get_PortraitPath")]
public static class JainaAncientPortraitPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardModel __instance, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }
            __result = JainaCardStyle.ResolvePortrait(__result, __instance.Rarity == CardRarity.Ancient);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] ancient portrait swap failed: {ex.Message}");
        }
    }
}

/// <summary>
/// 图鉴大图（CardModel.BigPortrait）也要跟随卡图风格。
/// 它取的是 PortraitPngPath（packed/card_portraits/...）或 Portrait，不是 CustomPortraitPath，
/// 且 PortraitPath 已被 JainaAncientPortraitPatch 改过——所以这里必须用**未被修改的** CustomPortraitPath
/// 作为基准再解析一次，否则会误判"无需替换"而保留原版大图。
/// 证据：CardModel.BigPortrait（0.113 src/Core/Models/CardModel.cs:185-193）、
/// NCard.SetPortraitTexture（NCard.cs:847）、图鉴调用点 NInspectCardScreen.cs:247。
/// </summary>
[HarmonyPatch(typeof(CardModel), "get_BigPortrait")]
public static class JainaBigPortraitPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardModel __instance, ref Texture2D __result)
    {
        try
        {
            if (__instance is not IModCardAssetOverrides overrides)
            {
                return;
            }
            var basePath = overrides.CustomPortraitPath;
            if (string.IsNullOrEmpty(basePath))
            {
                return;
            }
            var resolved = JainaCardStyle.ResolvePortrait(basePath, __instance.Rarity == CardRarity.Ancient);
            if (string.IsNullOrEmpty(resolved) || resolved == basePath)
            {
                return;
            }
            var tex = ResourceLoader.Load<Texture2D>(resolved);
            if (tex != null)
            {
                __result = tex;
            }
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] big portrait swap failed: {ex.Message}");
        }
    }
}
