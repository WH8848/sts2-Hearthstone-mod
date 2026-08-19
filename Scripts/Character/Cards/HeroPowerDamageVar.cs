using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Powers;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 英雄技能伤害变量：DamageVar 子类，显示值实时叠加灌注层数与野火加成
/// （与 OnPlay 实际结算一致）。
/// 必须继承 DamageVar 而非替换为 ComputedDynamicVar：
/// DynamicVarSet.Damage 强转 DamageVar，ComputedDynamicVar 会抛
/// InvalidCastException（牌库网格初始化升级卡牌时即崩溃，卡牌全堆在左上角）。
/// BaseValue 保持真实存储值（1 + 升级等级），仅显示路径叠加加成。
/// </summary>
public sealed class HeroPowerDamageVar : DamageVar
{
    public HeroPowerDamageVar(decimal baseValue)
        : base(baseValue, ValueProp.Move)
    {
    }

    /// <summary>
    /// 显示路径（无 formatter 的 {Damage} 等 IConvertible 取值）：
    /// 基础 + 灌注/野火加成。
    /// </summary>
    protected override decimal GetBaseValueForIConvertible()
    {
        return BaseValue + GetBonus(_owner as CardModel);
    }

    /// <summary>
    /// 预览路径（{Damage:diff()} 取 PreviewValue）：
    /// 先跑原版 enchantment/力量等 hooks，再叠加灌注/野火加成。
    /// </summary>
    public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        base.UpdateCardPreview(card, previewMode, target, runGlobalHooks);
        base.PreviewValue += GetBonus(card);
    }

    /// <summary>当前灌注 + 野火加成（战斗外为 0）。</summary>
    private static decimal GetBonus(CardModel? card)
    {
        if (card?.Owner?.Creature?.CombatState == null)
        {
            return 0m;
        }
        var empower = card.Owner.Creature.GetPower<EmpowerPower>();
        var wildfire = card.Owner.Creature.GetPower<WildfirePower>();
        return (empower?.EmpowerStacks ?? 0) + (wildfire?.WildfireStacks ?? 0);
    }
}
