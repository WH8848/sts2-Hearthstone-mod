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
                // 诊断（仅用户反馈"类型牌框太小"的卡 + 同批正常卡作对照）
                var entry = __instance.Model.Id.Entry;
                if (entry is "JAINA_CARD_DEATHBORNE_CARD" or "JAINA_CARD_FORBIDDEN_SHRINE_CARD"
                    or "JAINA_CARD_APEXIS_BLAST" or "JAINA_CARD_CRAM_SESSION_CARD")
                {
                    var plaque = __instance.GetNodeOrNull<NinePatchRect>("%TypePlaque");
                    MegaCrit.Sts2.Core.Logging.Log.Info(
                        $"[JainaPlaque] {entry} ready={__instance.IsNodeReady()} " +
                        $"labelSize={label.Size} labelMin={label.GetCombinedMinimumSize()} " +
                        $"plaqueSize={plaque?.Size.ToString() ?? "null"} text='{label.Text}'");
                }
            }
            catch
            {
                // 展示层补丁：任何异常都不影响原版标签
            }
        }

        /// <summary>
        /// 把类型标签撑到其最小宽度（长文案所需宽度），并按原版公式同步类型牌宽度、
        /// 保持中心点不动。仅在 label 被压扁（图鉴网格）时生效，正常路径无变化。
        /// </summary>
        private static void EnsureLabelAndPlaqueWidths(NCard card, MegaLabel label)
        {
            var min = label.GetCombinedMinimumSize();
            if (label.Size.X < min.X - 0.5f)
            {
                label.Size = new Vector2(min.X, label.Size.Y);
            }
            var plaque = card.GetNodeOrNull<NinePatchRect>("%TypePlaque");
            if (plaque == null)
            {
                return;
            }
            float want = Mathf.Max(label.Size.X + 17f, 61f);
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
