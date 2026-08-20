using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 条件攻击意图：继承 <see cref="SingleAttackIntent"/>（而非包装），
/// 因此 0.111.1 的 NIntent 会走 `intent is AttackIntent` 分支渲染**攻击数值标签**
/// （intents 表 FORMAT_DAMAGE_SINGLE = "{Damage}"，意图图标旁显示攻击力数字）。
/// 只有当 <c>isVisible</c> 委托返回 true 时才显示攻击意图；
/// 条件不成立时视觉退化为游戏原生的"隐藏意图"（intent_hidden 图标 + 无粒子 + 无悬停提示 + 空标签）。
/// 求值发生在意图 UI 刷新时（UpdateVisuals），调用方在状态变化后
/// 触发 <see cref="JainaMinionBase.RefreshIntentDisplay"/> 即可让意图即时出现/消失。
/// </summary>
public sealed class JainaConditionalAttackIntent : SingleAttackIntent
{
    private readonly Func<bool> _isVisible;

    public JainaConditionalAttackIntent(Func<decimal> damageCalc, Func<bool> isVisible)
        : base(damageCalc)
    {
        _isVisible = isVisible;
    }

    /// <summary>
    /// 隐藏状态下不显示悬停提示
    /// </summary>
    public override bool HasIntentTip => _isVisible() && base.HasIntentTip;

    /// <summary>
    /// 动画：可攻击 → 攻击分级动画（attack_N）；不可攻击 → 隐藏动画
    /// </summary>
    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return _isVisible() ? base.GetAnimation(targets, owner) : "hidden";
    }

    /// <summary>
    /// 粒子纹理：不可攻击时清空（粒子消失）
    /// </summary>
    public override Texture2D? GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return _isVisible() ? base.GetTexture(targets, owner) : null;
    }

    /// <summary>
    /// 数值标签：可攻击 → 攻击力数字（FORMAT_DAMAGE_SINGLE = {Damage}）；
    /// 不可攻击 → 空标签
    /// </summary>
    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        return _isVisible() ? base.GetIntentLabel(targets, owner) : new LocString("intents", "FORMAT_EMPTY");
    }

    /// <summary>
    /// 意图伤害预览 = 随从攻击<b>实际目标（敌人）</b>时将造成的伤害（含易伤/力量/冻结等修正）。
    /// 原版 <see cref="AttackIntent.GetSingleDamage"/> 把 target 硬编码为玩家自己
    /// （me.Creature）——那是怪物语义（怪物总是攻击玩家）；随从是玩家侧单位，
    /// 攻击目标是敌人，target 相关修正（易伤 Vulnerable、无实体 Intangible 等）
    /// 必须按敌人计算，否则：敌人有易伤时意图偏低（显示攻击力而实际 ×1.5）、
    /// 玩家自己被上易伤时意图虚高（错误 ×1.5）。
    /// </summary>
    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner)
    {
        try
        {
            if (DamageCalc == null || owner == null || owner.CombatState == null)
            {
                return base.GetTotalDamage(targets, owner);
            }
            // 实际攻击目标：优先取传入目标中的存活敌人（手动点击选中目标）；
            // 意图刷新传入的是玩家（RefreshIntents 原版语义），回退取第一个可命中敌人。
            var target = targets.FirstOrDefault(t => t != null && t.IsAlive && t.IsHittable &&
                                                     t.Side != owner.Side);
            if (target == null)
            {
                target = owner.CombatState
                    .GetOpponentsOf(owner)
                    .FirstOrDefault(e => e != null && e.IsAlive && e.IsHittable);
            }
            if (target == null)
            {
                return base.GetTotalDamage(targets, owner);
            }
            var me = LocalContext.GetMe(owner.CombatState);
            if (me == null)
            {
                return base.GetTotalDamage(targets, owner);
            }
            decimal dmg = Hook.ModifyDamage(
                me.RunState, me.Creature.CombatState, target, owner, DamageCalc(),
                ValueProp.Move, null, null, ModifyDamageHookType.All,
                CardPreviewMode.None, out _);
            return Math.Max(0, (int)dmg);
        }
        catch
        {
            // 预览失败回退原版逻辑，不影响意图显示
            return base.GetTotalDamage(targets, owner);
        }
    }
}
