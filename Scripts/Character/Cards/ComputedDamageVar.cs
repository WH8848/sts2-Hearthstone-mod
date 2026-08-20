using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 动态伤害变量（DamageVar 子类）：显示值由委托按卡实例实时计算。
/// <b>为什么必须继承 DamageVar 而不是用 RitsuLib 的 ComputedDynamicVar 放 "Damage" 槽：</b>
/// 游戏原版 <c>DynamicVarSet.Damage</c> 是 <c>(DamageVar)_vars["Damage"]</c> 强转——
/// ComputedDynamicVar 放进 "Damage" 槽后，任何访问 <c>card.DynamicVars.Damage</c> 的地方
/// （打出流程、附魔应用/预览、牌库网格渲染、升级预览）都会抛 InvalidCastException：
/// 表现为"卡打出没造成伤害直接进弃牌堆"（强能奥术飞弹）、"技能牌不能被附魔"
/// （附魔界面渲染候选卡时崩溃）、"源生之石打出即异常"（其自动使用其余选项的卡崩冒泡）。
/// 本类以 DamageVar 为基类 + 计算委托，强转安全，同时保留动态显示与升级预览。
/// </summary>
public sealed class ComputedDamageVar : DamageVar
{
    private readonly Func<CardModel, decimal> _compute;

    /// <param name="baseValue">基础值（升级预览/附魔计算用，通常是未升级时的静态值）</param>
    /// <param name="compute">动态显示值（card 为当前卡实例；canonical 不可变实例访问 Owner 会抛异常，委托内需自行判 IsMutable）</param>
    public ComputedDamageVar(decimal baseValue, Func<CardModel, decimal> compute)
        : base(baseValue, ValueProp.Move)
    {
        _compute = compute;
    }

    /// <summary>
    /// 显示路径（无 formatter 的 {Damage} 等 IConvertible 取值）：委托计算值。
    /// </summary>
    protected override decimal GetBaseValueForIConvertible()
    {
        return _compute(_owner as CardModel);
    }

    /// <summary>
    /// 预览路径（{Damage:diff()} 取 PreviewValue）：
    /// 先跑原版 base（附魔加成/力量等全局 hooks 作用于 BaseValue），
    /// 再以"动态值替换基础值"的方式叠加委托计算值——附魔/力量对基础部分的
    /// 加成保留，动态部分（升级形态/施放次数/派系加成等）实时跟随。
    /// </summary>
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        base.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
        if (card == null)
        {
            return;
        }
        try
        {
            base.PreviewValue = base.PreviewValue - base.BaseValue + _compute(card);
        }
        catch
        {
            // canonical（图鉴渲染等）不可变：访问 Owner 抛异常，保持 base 结果
        }
    }
}
