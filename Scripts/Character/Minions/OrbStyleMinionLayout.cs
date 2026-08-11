using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MinionLib.Layout;
using MinionLib.Minion;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 吉安娜随从布局 - 将随从锚定在玩家角色（充能球区域）周围。
/// 按照从数量排成一圈/一排，紧贴玩家，模拟充能球环绕效果。
/// </summary>
public sealed class OrbStyleMinionLayout : IMinionLayout
{
    /// <summary>
    /// 随从缩放比例（缩小以适配充能球位置）
    /// </summary>
    private const float MinionScale = 0.35f;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsActive => true;

    /// <summary>
    /// 计算布局：把所有未放置的随从放到玩家身边
    /// </summary>
    public void ApplyLayout(MinionLayoutContext context)
    {
        var unhandled = context.UnhandledMinions.ToList();
        if (unhandled.Count == 0)
        {
            return;
        }

        // 按宠物主人分组
        var groups = unhandled
            .GroupBy(m => m.Entity.PetOwner)
            .ToList();

        foreach (var group in groups)
        {
            var ownerNode = context.Room.GetCreatureNode(group.Key!.Creature);
            if (ownerNode == null)
            {
                continue;
            }

            var minions = group.ToList();
            var ownerPos = ((Control)ownerNode).Position;

            // 缩小模型（同步，无动画）
            for (int i = 0; i < minions.Count; i++)
            {
                minions[i].SetScaleAndHue(MinionScale, 0f);
            }

            // 参考原版充能球布局：以玩家为圆心、头顶高度（充能球位置）弧形展开。
            // 充能球实际以玩家位置 + (225~300, 上) 为圆心排布，
            // 这里把圆心抬到充能球高度（-210f），使随从位于充能球同一高度的弧线上。
            int count = minions.Count;
            float angleSpan = 125f;
            float step = angleSpan / Mathf.Max(1f, count - 1f);
            float radius = Mathf.Lerp(225f, 300f, ((float)count - 3f) / 7f);

            // 圆心：玩家位置向上偏移到充能球高度
            Vector2 center = ownerPos + new Vector2(0f, -210f);

            for (int i = 0; i < count; i++)
            {
                float s = float.DegreesToRadians(-25f - (angleSpan - i * step));
                Vector2 offset = new Vector2(-Mathf.Cos(s), Mathf.Sin(s)) * radius;
                context.Positions[minions[i]] = center + offset;
            }
        }
    }
}