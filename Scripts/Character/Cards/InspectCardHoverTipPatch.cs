using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 卡牌详情界面（图鉴 / 牌组查看）的悬停提示分列。
///
/// 原版 NHoverTipSet 只有一列提示：放不下时 CorrectHorizontalOverflow 会把整组提示
/// 一起翻到卡牌左侧，随从卡（提示组最多）会整片遮住左侧列表。
/// 本补丁改为"右侧放得下的留在右列，放不下的顺移到左列"：
/// - 右列仍挂在 HoverTipRect 右侧，宽度 360；
/// - 左列是动态创建的 VFlowContainer，挂在 HoverTipRect 左侧；
/// - 总高度放得下时不创建左列，完全保持原版表现。
/// 只对名字为 HoverTipRect 的节点生效（图鉴大卡详情）。
/// </summary>
public static class InspectCardHoverTipPatch
{
    private const string HoverTipRectName = "HoverTipRect";

    private const string LeftColumnName = "JainaLeftTipColumn";

    /// <summary>提示列宽（与原版 hover_tip 一致）</summary>
    private const float TipWidth = 360f;

    /// <summary>两列与卡牌之间的间距</summary>
    private const float TipGap = 10f;

    /// <summary>底部留白</summary>
    private const float BottomMargin = 24f;

    [HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.SetAlignment))]
    private static class SetAlignmentPostfix
    {
        private static void Postfix(NHoverTipSet __instance, Control node)
        {
            try
            {
                if (node == null || node.Name.ToString() != HoverTipRectName)
                {
                    return;
                }
                var text = Traverse.Create(__instance)
                    .Field("_textHoverTipContainer")
                    .GetValue<VFlowContainer>();
                if (text == null)
                {
                    return;
                }
                var viewport = node.GetViewport();
                if (viewport == null)
                {
                    return;
                }

                Vector2 viewSize = viewport.GetVisibleRect().Size;
                var left = __instance.GetNodeOrNull<VFlowContainer>(LeftColumnName);

                // 先把左列里的提示搬回右列，统一重新分配（幂等）
                if (left != null)
                {
                    var moved = new List<Node>();
                    foreach (Node n in left.GetChildren())
                    {
                        moved.Add(n);
                    }
                    foreach (Node n in moved)
                    {
                        left.RemoveChild(n);
                        text.AddChild(n);
                    }
                }

                var tips = new List<Control>();
                foreach (Node n in text.GetChildren())
                {
                    if (n is Control c)
                    {
                        tips.Add(c);
                    }
                }
                if (tips.Count == 0)
                {
                    if (left != null)
                    {
                        left.QueueFree();
                    }
                    return;
                }

                // 个别提示会被按不换行文本撑宽：此时不做分列，保留原版行为
                float maxTipWidth = tips.Max(c => c.Size.X);
                if (maxTipWidth > TipWidth + 1f)
                {
                    if (left != null)
                    {
                        left.QueueFree();
                    }
                    Entry.Logger.Info(
                        $"[JainaHover] wide tip column ({maxTipWidth:F0} > {TipWidth:F0}), keep vanilla layout");
                    return;
                }

                // 右侧可用高度：HoverTipRect 顶部到屏幕底部
                float topY = node.GlobalPosition.Y;
                float rectX = node.GlobalPosition.X;
                float rectRight = rectX + node.Size.X;

                // 衍生物卡牌：使用原版位置（NHoverTipSet.SetAlignment 已将其放在 HoverTipRect 左侧），
                // 只恢复 card_hover_tip.tscn 的 0.75 缩放（NCard 池化复用会把 scale 重置为 1）
                var cardTips = Traverse.Create(__instance)
                    .Field("_cardHoverTipContainer")
                    .GetValue<NHoverTipCardContainer>();
                float cardHeight = 0f;
                float leftColumnX = rectX - TipWidth - TipGap;
                if (cardTips != null && cardTips.Size.X > 0f)
                {
                    foreach (Node tipNode in cardTips.GetChildren())
                    {
                        if (tipNode.GetNodeOrNull<Control>("Card") is Control cardNode)
                        {
                            cardNode.Scale = Vector2.One * 0.75f;
                        }
                    }
                    // 左列提示词右框与衍生物卡牌右卡框对齐（原版卡牌右框 = HoverTipRect 左边缘）
                    leftColumnX = Mathf.Max(8f, rectX - TipWidth);
                    cardHeight = cardTips.Size.Y + 8f;
                }
                float leftColumnTop = topY + cardHeight;
                float maxHeight = viewSize.Y - topY - BottomMargin;
                if (maxHeight < 240f)
                {
                    maxHeight = viewSize.Y - BottomMargin;
                }

                float totalHeight = tips.Sum(c => c.Size.Y + 5f);
                if (totalHeight <= maxHeight)
                {
                    // 右侧放得下：不创建左列，保持原版表现
                    if (left != null)
                    {
                        left.QueueFree();
                    }
                    return;
                }

                // 需要分列：右侧按顺序尽量多放，其余移到左列
                if (left == null)
                {
                    left = new VFlowContainer
                    {
                        Name = LeftColumnName,
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    };
                    __instance.AddChild(left);
                }

                float rightHeight = 0f;
                float leftHeight = 0f;
                bool rightFull = false;
                foreach (var tip in tips)
                {
                    float h = tip.Size.Y + 5f;
                    if (!rightFull && rightHeight + h <= maxHeight)
                    {
                        rightHeight += h;
                        continue;
                    }
                    rightFull = true;
                    text.RemoveChild(tip);
                    left.AddChild(tip);
                    leftHeight += h;
                }

                text.Size = new Vector2(TipWidth, Mathf.Max(rightHeight, 1f));
                text.GlobalPosition = new Vector2(rectRight + TipGap, topY);
                left.Size = new Vector2(TipWidth, Mathf.Max(leftHeight, 1f));
                left.GlobalPosition = new Vector2(leftColumnX, leftColumnTop);

                Entry.Logger.Info(
                    $"[JainaHover] split tips: viewport={viewSize.X:F0}x{viewSize.Y:F0} " +
                    $"rectX={rectX:F0} rectRight={rectRight:F0} maxH={maxHeight:F0} " +
                    $"total={totalHeight:F0} right={rightHeight:F0} left={leftHeight:F0} " +
                    $"rightCount={text.GetChildCount()} leftCount={left.GetChildCount()}");
            }
            catch
            {
                // 展示层补丁：任何异常都不影响原版提示
            }
        }
    }
}
