using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using jaina.Scripts.Character.Cards;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 吉安娜手牌发光统一处理。
/// 原版 NHandCardHolder.UpdateCard 会给所有可打出的卡设置青色高亮
/// （CardHighlight.playableColor），用户期望"只有对局内衍生出来的卡才发光"。
/// 此 patch 在 UpdateCard 之后（Priority.Last）对 Jaina 卡：
/// - 衍生卡（JainaCastTracker.IsGeneratedCard）：显示浓天蓝色高亮
/// - 非衍生卡：隐藏高亮（覆盖原版青色可打出高亮，避免"所有手牌发光"）
/// 原版卡不受影响。
/// </summary>
[HarmonyPatch(typeof(NHandCardHolder), nameof(NHandCardHolder.UpdateCard))]
public static class JainaHandGlowPatch
{
    /// <summary>浓天蓝（DeepSkyBlue 系）</summary>
    private static readonly Godot.Color SkyBlue = new(0f, 0.75f, 1f);

    private static void Postfix(NHandCardHolder __instance)
    {
        var model = __instance.CardNode?.Model;
        if (model is not JainaSpellCardTemplate and not JainaMinionCardTemplate)
        {
            return;
        }
        var highlight = __instance.CardNode?.CardHighlight;
        if (highlight == null)
        {
            return;
        }
        if (jaina.Scripts.Character.JainaCastTracker.IsGeneratedCard(model))
        {
            highlight.AnimShow();
            highlight.Modulate = SkyBlue;
        }
        else
        {
            highlight.AnimHide();
        }
    }
}
