using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 条件攻击意图：包装一个 <see cref="SingleAttackIntent"/>，
/// 只有当 <c>isVisible</c> 委托返回 true 时才显示攻击意图；
/// 条件不成立时视觉退化为游戏原生的"隐藏意图"（intent_hidden 图标 + 无粒子 + 无悬停提示）。
/// 求值发生在意图 UI 刷新时（UpdateVisuals），因此调用方在状态变化后
/// 触发 <see cref="JainaMinionBase.RefreshIntentDisplay"/> 即可让意图即时出现/消失。
/// </summary>
public sealed class JainaConditionalAttackIntent : AbstractIntent
{
    private readonly SingleAttackIntent _inner;
    private readonly Func<bool> _isVisible;

    public JainaConditionalAttackIntent(SingleAttackIntent inner, Func<bool> isVisible)
    {
        _inner = inner;
        _isVisible = isVisible;
    }

    /// <summary>
    /// 可攻击时表现为攻击意图；不可攻击时表现为隐藏意图
    /// </summary>
    public override IntentType IntentType => _isVisible() ? _inner.IntentType : IntentType.Hidden;

    /// <summary>
    /// 隐藏状态下不显示悬停提示
    /// </summary>
    public override bool HasIntentTip => _isVisible() && _inner.HasIntentTip;

    /// <summary>
    /// 悬停提示文案使用攻击意图的词条（仅可攻击时被查询）
    /// </summary>
    protected override string IntentPrefix => "ATTACK";

    /// <summary>
    /// 静态资源收集不需要额外路径（攻击动画帧由游戏全局 IntentAnimData 预加载）
    /// </summary>
    protected override string? SpritePath => null;

    /// <summary>
    /// 动画：可攻击 → 攻击分级动画；不可攻击 → 隐藏动画
    /// </summary>
    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner)
    {
        return _isVisible() ? _inner.GetAnimation(targets, owner) : "hidden";
    }

    /// <summary>
    /// 粒子纹理：不可攻击时清空（粒子消失）
    /// </summary>
    public override Texture2D? GetTexture(IEnumerable<Creature> targets, Creature owner)
    {
        return _isVisible() ? _inner.GetTexture(targets, owner) : null;
    }

    /// <summary>
    /// 数值标签：不可攻击时返回空标签
    /// </summary>
    public override LocString GetIntentLabel(IEnumerable<Creature> targets, Creature owner)
    {
        return _isVisible() ? _inner.GetIntentLabel(targets, owner) : base.GetIntentLabel(targets, owner);
    }
}
