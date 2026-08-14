using System;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 巫师学徒光环：当 4 只巫师学徒同时在场时，你的法术牌（攻击牌/技能牌）费用减少 1 点。
/// 挂在随从生物自身——随从死亡时本 Power 随生物移除。
/// 减费只由场上第一只学徒的光环执行（其余返回 false），保证多只在场时总量恰好 -1。
/// </summary>
[RegisterPower]
public sealed class SorcererApprenticePower : PowerModel
{
    /// <summary>触发减费需要的巫师学徒数量</summary>
    private const int RequiredApprentices = 4;

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

        // 当 4 只巫师学徒在场时才减 1 费
        var apprentices = owner.PlayerCombatState?.Pets
            .Where(p => p != null && p.IsAlive && p.Monster is SorcererApprenticeMinion)
            .ToList();
        if (apprentices == null || apprentices.Count < RequiredApprentices)
        {
            return false;
        }
        // 只有场上第一只学徒的光环执行减费（其余不执行），保证总量恰好 -1
        if (apprentices[0] != Owner)
        {
            return false;
        }

        modifiedCost = Math.Max(0, originalCost - 1);
        return true;
    }
}
