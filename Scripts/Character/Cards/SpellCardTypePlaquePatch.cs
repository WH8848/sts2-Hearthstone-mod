using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Cards;
using jaina.Scripts.Character.Keywords;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 法术牌/任务牌卡面类型标签：
/// 带"法术牌"内部标记（JainaKeywords.Spell）或"任务"关键词（JainaKeywords.Quest）的卡，
/// 显示"基础类型丨法术"（攻击丨法术 / 技能丨法术 / 能力丨法术；
/// 任务线卡底层是能力类型 → 能力丨法术），分隔符统一使用"丨"。
/// 原版 NCard.UpdateTypePlaque 只显示基础类型（攻击/技能/能力/随从…）。
/// 后缀文本通过 gameplay_ui.json 的 CARD_TYPE.SPELL 本地化（zhs=法术，eng=Spell）。
/// </summary>
public static class SpellCardTypePlaquePatch
{
    /// <summary>
    /// 法术牌类型标签后缀（本地化键，见 gameplay_ui.json）
    /// </summary>
    private static readonly LocString SpellSuffixLoc = new LocString("gameplay_ui", "CARD_TYPE.SPELL");

    [HarmonyPatch(typeof(NCard), "UpdateTypePlaque")]
    private static class UpdateTypePlaquePostfix
    {
        private static void Postfix(NCard __instance)
        {
            try
            {
                if (__instance.Model == null || !__instance.IsNodeReady())
                {
                    return;
                }
                var keywords = __instance.Model.Keywords;
                if (!keywords.Contains(JainaKeywords.Spell) && !keywords.Contains(JainaKeywords.Quest))
                {
                    return;
                }
                var label = __instance.GetNode<MegaLabel>("%TypeLabel");
                if (label == null)
                {
                    return;
                }
                // 基础类型文本（攻击/技能/能力…）+ "丨法术"（分隔符用中文竖线"丨"）
                var baseText = __instance.Model.Type.ToLocString().GetFormattedText();
                label.SetTextAutoSize(baseText + "丨" + SpellSuffixLoc.GetFormattedText());
            }
            catch
            {
                // 展示层补丁：任何异常都不影响原版标签
            }
        }
    }
}
