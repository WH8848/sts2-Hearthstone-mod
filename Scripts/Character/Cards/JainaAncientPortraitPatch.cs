using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 先古卡样式开启时，把吉安娜卡的卡图路径从 assets/card_art/ 换成 assets/card_art_ancient/
/// （250x351 竖版）。
/// 证据：CardModel.PortraitPath 是 public virtual string（0.113 src/Core/Models/CardModel.cs:165），
/// CardModel.Portrait = ResourceLoader.Load&lt;Texture2D&gt;(PortraitPath)（同文件 179）；
/// RitsuLib 用 CustomPortraitPath 覆盖 __result（ContentAssetModelVisualOverridePatches.cs:153/216），
/// 所以这里用 Priority.Last 在其之后再改一次。
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
            if (!JainaCardStyle.Ancient)
            {
                return;
            }
            if (__instance.VisualCardPool is not (JainaCardPool or JainaNeutralCardPool))
            {
                return;
            }
            __result = JainaCardStyle.ToAncientPath(__result);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] ancient portrait swap failed: {ex.Message}");
        }
    }
}
