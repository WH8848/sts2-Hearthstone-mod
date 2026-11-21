using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

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
            __result = JainaCardStyle.ResolvePortrait(__result);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] ancient portrait swap failed: {ex.Message}");
        }
    }
}
