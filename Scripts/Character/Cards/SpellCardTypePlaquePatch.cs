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

    /// <summary>
    /// 已输出过诊断的键（避免每帧重复打印）
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> LoggedKeys = [];

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
                // 图鉴网格里 label.Size 可能停留在旧文案宽度（44，"攻击"两字宽），
                // 而新文案的最小宽度已是 80 —— 原版框宽公式 max(label.Size.X + 17, 61)
                // 会得到 61 的小框（文字溢出）；放大预览路径布局正常则无此问题。
                // 这里把 label 撑到最小宽度并同步框宽（结果与其它卡一致，不额外放大）。
                EnsureLabelAndPlaqueWidths(__instance, label);
                Callable.From(() =>
                {
                    try
                    {
                        if (GodotObject.IsInstanceValid(__instance) && GodotObject.IsInstanceValid(label))
                        {
                            EnsureLabelAndPlaqueWidths(__instance, label);
                        }
                    }
                    catch
                    {
                        // 展示层补丁：忽略
                    }
                }).CallDeferred();
                // 兜底告警：任何法术卡若框宽仍小于文本需求（min + 17），打出日志便于复查
                var plaqueCheck = __instance.GetNodeOrNull<NinePatchRect>("%TypePlaque");
                var minCheck = label.GetCombinedMinimumSize();
                if (plaqueCheck != null && plaqueCheck.Size.X + 0.5f < minCheck.X + 17f)
                {
                    MegaCrit.Sts2.Core.Logging.Log.Warn(
                        $"[JainaPlaque] NARROW {__instance.Model.Id.Entry} plaque={plaqueCheck.Size.X} " +
                        $"need={minCheck.X + 17f} labelSize={label.Size.X} text='{label.Text}'");
                }
            }
            catch
            {
                // 展示层补丁：任何异常都不影响原版标签
            }
        }

        /// <summary>
        /// 把类型标签撑到其<b>真实文本宽度</b>并按原版公式同步类型牌宽度、保持中心点不动。
        /// 宽度取值顺序（取最大）：字体实测宽度（Label theme font 的 GetStringSize）＞
        /// 按字号估算（中文 1em、西文 0.55em）＞ label 最小尺寸。
        /// <b>为什么不能用 label.Size / GetCombinedMinimumSize</b>：图鉴网格里 label 被压到旧宽度 44，
        /// 而 GetCombinedMinimumSize 返回 80 仍小于实际渲染宽度（"技能丨法术"5 字按 20px 字号约 100px），
        /// 框宽按 80+17=97 仍会溢出（实测截图文字超出右边框）。
        /// </summary>
        private static void EnsureLabelAndPlaqueWidths(NCard card, MegaLabel label)
        {
            var text = label.Text ?? string.Empty;
            var min = label.GetCombinedMinimumSize();
            int fontSize = 20;
            float measured = 0f;
            try
            {
                var font = label.GetThemeFont("font");
                var fsize = label.GetThemeFontSize("font_size");
                if (fsize > 0)
                {
                    fontSize = fsize;
                }
                if (font != null)
                {
                    measured = font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
                }
            }
            catch
            {
                // 取字体失败则用估算兜底
            }
            // need 优先取"字体实测宽度"，其次 label 最小尺寸；两者都不可用时才按字号估算
            float need = Mathf.Max(min.X, measured);
            if (need <= 0.5f)
            {
                float estimated = 0f;
                foreach (var ch in text)
                {
                    estimated += ch >= 0x2E80 ? fontSize : fontSize * 0.55f;
                }
                need = estimated;
            }
            if (label.Size.X < need - 0.5f)
            {
                label.Size = new Vector2(need, label.Size.Y);
            }
            var plaque = card.GetNodeOrNull<NinePatchRect>("%TypePlaque");
            if (plaque == null)
            {
                return;
            }
            float want = Mathf.Max(need + 17f, 61f);
            // 详细诊断（每张卡每种结果只打一次，避免刷屏）：真实文本宽度/需求/框宽
            try
            {
                var key = card.Model.Id.Entry + "|" + ((int)want) + "|" + ((int)need);
                if (LoggedKeys.Add(key))
                {
                    MegaCrit.Sts2.Core.Logging.Log.Info(
                        $"[JainaPlaque] {card.Model.Id.Entry} text='{text}' min={min.X} measured={measured} " +
                        $"need={need} want={want} plaqueBefore={plaque.Size.X} labelBefore={label.Size.X} fontSize={fontSize}");
                }
            }
            catch
            {
                // 诊断失败忽略
            }
            if (Mathf.Abs(plaque.Size.X - want) < 0.5f)
            {
                return;
            }
            float centerX = plaque.Position.X + plaque.Size.X * 0.5f;
            plaque.Size = new Vector2(want, plaque.Size.Y);
            plaque.Position = new Vector2(centerX - want * 0.5f, plaque.Position.Y);
        }
    }
}
