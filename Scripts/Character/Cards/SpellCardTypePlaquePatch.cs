using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Cards;
using jaina.Scripts.Character.Keywords;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 法术牌卡面类型标签：攻击|法术 / 技能|法术 / 能力|法术。
/// 原版 NCard.UpdateTypePlaque 只显示基础类型（攻击/技能/能力/随从…），
/// 带"法术牌"内部标记关键词（JainaKeywords.Spell）的卡在其后追加"|法术"，
/// 让玩家从卡面类型牌匾即可看出这是一张法术牌（而"法术牌"不再作为关键词展示）。
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
                if (!__instance.Model.Keywords.Contains(JainaKeywords.Spell))
                {
                    return;
                }
                var label = __instance.GetNode<MegaLabel>("%TypeLabel");
                if (label == null)
                {
                    return;
                }
                // 基础类型文本（攻击/技能/能力…）+ "|法术"
                var baseText = __instance.Model.Type.ToLocString().GetFormattedText();
                label.SetTextAutoSize(baseText + "|" + SpellSuffixLoc.GetFormattedText());
            }
            catch
            {
                // 展示层补丁：任何异常都不影响原版标签
            }
        }
    }
}
