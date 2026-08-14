using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

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
}
