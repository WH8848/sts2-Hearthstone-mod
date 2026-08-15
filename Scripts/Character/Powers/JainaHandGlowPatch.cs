using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using jaina.Scripts.Character.Cards;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 吉安娜手牌发光：不修改原版行为（可打出卡仍显示原版青色高亮），
/// 仅给本局对战内衍生出来的 Jaina 卡额外覆盖为更浓的天蓝色高亮。
/// 此 patch 在 UpdateCard 之后执行：衍生卡 Modulate 覆盖为浓天蓝；
/// 非衍生卡不做任何改动（保留原版青色高亮）。
/// </summary>
[HarmonyPatch(typeof(NHandCardHolder), nameof(NHandCardHolder.UpdateCard))]
public static class JainaHandGlowPatch
{
    /// <summary>浓天蓝（DeepSkyBlue 系，比原版青色 playableColor 更浓）</summary>
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
        // 仅衍生卡：覆盖为浓天蓝（其余卡保留原版青色高亮，不做改动）
        if (jaina.Scripts.Character.JainaCastTracker.IsGeneratedCard(model))
        {
            highlight.AnimShow();
            highlight.Modulate = SkyBlue;
        }
    }
}
