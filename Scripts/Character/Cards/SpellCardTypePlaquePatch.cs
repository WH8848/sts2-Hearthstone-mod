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
///
/// <b>类型牌尺寸</b>：原版类型牌按基础类型（两字）定宽（最小 61px），追加"丨法术"后
/// 文字超出小框（文字被挤压/放不下）。这里在改文案后同步放大类型牌（宽度按文字量估算，
/// 保持原中心点不动），并在下一帧（原版 UpdateTypePlaqueSizeAndPosition 的 deferred
/// 更新之后）再校正一次，避免被原版尺寸回写覆盖。
/// </summary>
public static class SpellCardTypePlaquePatch
{
    /// <summary>
    /// 法术牌类型标签后缀（本地化键，见 gameplay_ui.json）
    /// </summary>
    private static readonly LocString SpellSuffixLoc = new LocString("gameplay_ui", "CARD_TYPE.SPELL");

    /// <summary>
    /// 类型牌最小宽度（原版 NCard._typePlaqueMinXSize = 61）
    /// </summary>
    private const float MinPlaqueWidth = 61f;

    /// <summary>
    /// 文字左右内边距（原版 = label 宽 + 17）
    /// </summary>
    private const float PlaquePadding = 17f;

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
                ResizePlaque(__instance, label);
                // 下一帧（原版尺寸回写之后）再校正一次，保证长文案不被压缩
                Callable.From(() =>
                {
                    try
                    {
                        if (GodotObject.IsInstanceValid(__instance) && GodotObject.IsInstanceValid(label))
                        {
                            ResizePlaque(__instance, label);
                        }
                    }
                    catch
                    {
                        // 展示层补丁：忽略
                    }
                }).CallDeferred();
            }
            catch
            {
                // 展示层补丁：任何异常都不影响原版标签
            }
        }

        /// <summary>
        /// 按文字量放大类型牌并保持中心点不动。
        /// 宽度取三者最大：label 已布局宽度、label 最小宽度、按字符数估算宽度
        /// （MegaLabel 的 Size 可能仍保留基础类型时的旧值，故估算兜底；按语言自适应）。
        /// </summary>
        private static void ResizePlaque(NCard card, MegaLabel label)
        {
            var plaque = card.GetNodeOrNull<NinePatchRect>("%TypePlaque");
            if (plaque == null)
            {
                return;
            }
            var text = label.Text ?? string.Empty;
            // 字符宽度估算：中文约 1em、西文约 0.55em；字号取标签高度近似
            float em = Mathf.Max(label.Size.Y, 18f);
            float estimated = 0f;
            foreach (var ch in text)
            {
                estimated += ch >= 0x2E80 ? em : em * 0.56f;
            }
            float textWidth = Mathf.Max(label.Size.X, label.GetCombinedMinimumSize().X);
            textWidth = Mathf.Max(textWidth, estimated);
            float centerX = plaque.Position.X + plaque.Size.X * 0.5f;
            var newSize = plaque.Size;
            newSize.X = Mathf.Max(textWidth + PlaquePadding, MinPlaqueWidth);
            if (Mathf.IsEqualApprox(newSize.X, plaque.Size.X))
            {
                return;
            }
            plaque.Size = newSize;
            plaque.Position = new Vector2(centerX - newSize.X * 0.5f, plaque.Position.Y);
        }
    }
}
