using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 卡面派系标签：像炉石传说一样把法术派系（火焰/冰霜/奥术）
/// 显示在卡面最下方、卡框上面一点（描述文字下方固定位置）。
/// Patch NCard.UpdateVisuals（每次卡面刷新时同步文本/可见性）。
/// </summary>
[HarmonyPatch(typeof(NCard), "UpdateVisuals")]
public static class NCardSchoolLabelPatch
{
    /// <summary>
    /// 派系标签节点名
    /// </summary>
    public const string LabelName = "JainaSchoolLabel";

    public static void Postfix(NCard __instance)
    {
        var model = __instance.Model;
        var label = __instance.GetNodeOrNull<Label>(LabelName);

        if (model == null || !JainaCastTracker.TryGetSchool(model.GetType(), out var school))
        {
            // 非法术卡（或无派系）：隐藏标签（若已创建）
            if (label != null)
            {
                label.Visible = false;
            }
            return;
        }

        if (label == null)
        {
            label = new Label
            {
                Name = LabelName,
                // 卡面逻辑坐标系 300x422（Frame 中心锚定）：描述区底部 y=173，
                // 卡框底部 y=211——派系标签放在描述区下方、卡框内侧上方（y≈178~205）
                Position = new Vector2(-100f, 178f),
                Size = new Vector2(200f, 26f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", 13);
            __instance.AddChild(label);
        }

        label.Text = JainaCastTracker.GetSchoolDisplayName(school);
        label.Visible = true;
    }
}
