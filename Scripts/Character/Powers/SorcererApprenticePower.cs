using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 巫师学徒光环：你的法术牌（攻击牌/技能牌）费用减少1点。
/// 挂在随从生物自身——随从死亡时本 Power 随生物移除（减费随之失效）。
/// 通过费用修改钩子实时生效：主人法术牌的展示与结算费用统一 -1，下限为 0。
/// </summary>
[RegisterPower]
public sealed class SorcererApprenticePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        var owner = Owner?.PetOwner;
        if (owner == null || card.Owner != owner)
        {
            return false;
        }
        if (card.Type != CardType.Attack && card.Type != CardType.Skill)
        {
            return false;
        }
        if (originalCost <= 0)
        {
            return false;
        }
        modifiedCost = Math.Max(0, originalCost - 1);
        return true;
    }
}
