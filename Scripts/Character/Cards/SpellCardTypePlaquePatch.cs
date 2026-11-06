using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
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
    /// 原版 NCard 的牌框宽度常量（_typePlaqueXMargin / _typePlaqueMinXSize），重算时保持一致
    /// </summary>
    private const float PlaqueXMargin = 17f;

    private const float PlaqueMinXSize = 61f;

    /// <summary>
    /// card.tscn 中 TypeLabel 的默认半宽（offset_left=-22 / offset_right=22）。
    /// 非法术牌必须保持这个宽度：原版所有类型的牌框都按 44+17→61 计算，统一宽度。
    /// </summary>
    private const float DefaultTypeLabelHalfWidth = 22f;

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
                var label = __instance.GetNode<MegaLabel>("%TypeLabel");
                if (label == null)
                {
                    return;
                }

                if (!HasSpellPlaque(__instance.Model))
                {
                    // 非法术牌（攻击/技能/能力/英雄/随从/地标…）：
                    // NCard 在图鉴中是池化复用的 —— 同一节点可能刚显示过法术牌，
                    // TypeLabel 被撑宽到"攻击丨法术"的宽度；而 Label 不会随文本变短自动缩回，
                    // 原版 UpdateTypePlaqueSizeAndPosition 又会读 _typeLabel.Size.X 计算牌框宽度，
                    // 结果就是后面的非法术牌牌框被错误撑大。
                    // 这里显式把 Label 还原为 card.tscn 默认宽度（±22），让原版算回统一的 61。
                    ResetLabelWidth(label);
                    return;
                }

                // 基础类型文本（攻击/技能/能力…）+ "丨法术"（分隔符用中文竖线"丨"）
                var baseText = __instance.Model.Type.ToLocString().GetFormattedText();
                label.SetTextAutoSize(baseText + "丨" + SpellSuffixLoc.GetFormattedText());

                // 1) 同步把 Label 宽度对齐到新文本
                //    （原版紧随其后的 CallDeferred(UpdateTypePlaqueSizeAndPosition) 读 _typeLabel.Size.X）
                FitLabelWidth(label);
                // 2) 同步把牌框宽度也设好：原版 deferred 万一没执行（节点被移出/重排），
                //    牌框也不会停留在 tscn 默认的 61 宽
                FitPlaqueWidth(__instance, label);
                // 3) 再兜底一次：等原版 deferred 执行完后按实际文本重算（FIFO 保证在其后）
                RefitPlaqueLater(__instance);
            }
            catch
            {
                // 展示层补丁：任何异常都不影响原版标签
            }
        }
    }

    /// <summary>
    /// 是否为需要"XX丨法术"标签的卡（法术牌 / 任务牌）
    /// </summary>
    private static bool HasSpellPlaque(CardModel? model)
    {
        if (model == null)
        {
            return false;
        }
        var keywords = model.Keywords;
        return keywords.Contains(JainaKeywords.Spell) || keywords.Contains(JainaKeywords.Quest);
    }

    /// <summary>
    /// 把 TypeLabel 的宽度还原为 card.tscn 默认值（±22，宽 44）。
    /// 非法术牌用它抵掉池化复用时上一张法术牌残留的宽标签。
    /// </summary>
    private static void ResetLabelWidth(MegaLabel label)
    {
        if (Mathf.Abs(label.OffsetLeft + DefaultTypeLabelHalfWidth) < 0.01f &&
            Mathf.Abs(label.OffsetRight - DefaultTypeLabelHalfWidth) < 0.01f)
        {
            return;
        }
        label.OffsetLeft = -DefaultTypeLabelHalfWidth;
        label.OffsetRight = DefaultTypeLabelHalfWidth;
    }

    /// <summary>
    /// 把 TypeLabel 的宽度对齐到当前文本，并保持居中锚点对称
    /// （card.tscn 中 TypeLabel 锚点居中、offset 为 ±22）。
    ///
    /// 原版 NCard.UpdateTypePlaqueSizeAndPosition 用 <c>_typeLabel.Size.X + 17</c> 决定牌框宽度，
    /// 而 Label 只在 minimum size 刷新后才撑大尺寸；图鉴里在不可见状态下完成 Reload 的卡牌，
    /// 这一步可能在 deferred 计算时还没发生，牌框就按 tscn 默认值（44+17=61）算，
    /// "攻击丨法术"这类 5 字标签便会溢出到框外。
    ///
    /// 这里不依赖 Label 的尺寸刷新时机：直接用主题字体测量文本宽度
    /// （<see cref="Font.GetStringSize"/>，与可见性/布局无关）。
    /// </summary>
    private static void FitLabelWidth(MegaLabel label)
    {
        // 优先字体测量：Label 的 minimum size 可能是旧文本的残留（池化复用/未刷新），
        // 取 max 会把上一条文本的宽度带过来，因此只在测量失败时退回最小尺寸。
        float width = MeasureTextWidth(label);
        if (width <= 0f)
        {
            width = label.GetCombinedMinimumSize().X;
        }
        if (width <= 0f)
        {
            return;
        }
        // 保持居中锚点：左右 offset 对称，Label 始终位于牌框中心
        label.OffsetLeft = -width * 0.5f;
        label.OffsetRight = width * 0.5f;
    }

    /// <summary>
    /// 按标签实际文本宽度设置牌框宽度，并保持牌框中心不变
    /// （等价于原版 UpdateTypePlaqueSizeAndPosition 的几何计算）
    /// </summary>
    private static void FitPlaqueWidth(NCard card, MegaLabel label)
    {
        var plaque = card.GetNodeOrNull<NinePatchRect>("%TypePlaque");
        if (plaque == null)
        {
            return;
        }
        float width = Mathf.Max(label.Size.X + PlaqueXMargin, PlaqueMinXSize);
        float centerX = plaque.Position.X + plaque.Size.X * 0.5f;
        plaque.Size = new Vector2(width, plaque.Size.Y);
        plaque.Position = new Vector2(centerX - width * 0.5f, plaque.Position.Y);
    }

    /// <summary>
    /// 用当前主题字体测量文本宽度（不依赖节点可见性与布局刷新时机）
    /// </summary>
    private static float MeasureTextWidth(MegaLabel label)
    {
        string text = label.Text;
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }
        try
        {
            var font = label.GetThemeFont("font", "Label");
            int fontSize = label.GetThemeFontSize("font_size", "Label");
            if (font == null || fontSize <= 0)
            {
                return 0f;
            }
            return font.GetStringSize(text, HorizontalAlignment.Left, -1f, fontSize).X;
        }
        catch
        {
            // 字体尚未就绪：退回 0，由 GetCombinedMinimumSize 兜底
            return 0f;
        }
    }

    /// <summary>
    /// 兜底：在原版 UpdateTypePlaqueSizeAndPosition（同为 deferred，先入队）之后，
    /// 按标签实际文本宽度重设牌框宽度并保持中心不变。
    /// 注意此时卡牌可能已被池化复用为另一张牌：重新按"当前模型"判断类型，
    /// 非法术牌只还原默认标签宽度，绝不按法术牌宽度撑大。
    /// </summary>
    private static void RefitPlaqueLater(NCard card)
    {
        Callable.From(() =>
        {
            try
            {
                if (!GodotObject.IsInstanceValid(card) || !card.IsNodeReady())
                {
                    return;
                }
                var plaque = card.GetNodeOrNull<NinePatchRect>("%TypePlaque");
                var label = card.GetNodeOrNull<MegaLabel>("%TypeLabel");
                if (plaque == null || label == null)
                {
                    return;
                }

                if (!HasSpellPlaque(card.Model))
                {
                    // 已被复用为非法术牌：只还原标签宽度，牌框交给原版（61）
                    ResetLabelWidth(label);
                    return;
                }

                FitLabelWidth(label);
                float width = Mathf.Max(label.Size.X + PlaqueXMargin, PlaqueMinXSize);
                float centerX = plaque.Position.X + plaque.Size.X * 0.5f;
                plaque.Size = new Vector2(width, plaque.Size.Y);
                plaque.Position = new Vector2(centerX - width * 0.5f, plaque.Position.Y);
            }
            catch
            {
                // 展示层补丁：任何异常都不影响原版牌框
            }
        }).CallDeferred();
    }
}
