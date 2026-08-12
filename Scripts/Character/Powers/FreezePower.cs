using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 冻结：被冻结的角色攻击造成的伤害减少 25%，可叠加。
/// 最大可叠加 4 层，回合结束全部消失。
/// </summary>
[RegisterPower]
public sealed class FreezePower : PowerModel
{
    /// <summary>
    /// 最大冻结层数
    /// </summary>
    public const int MaxStacks = 4;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 冻结者攻击时，其造成的伤害减少 25% × 层数（最多 4 层 = 减 100%）
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner)
        {
            return 1m;
        }
        if (!props.IsPoweredAttack())
        {
            return 1m;
        }
        return Math.Max(0m, 1m - 0.25m * Amount);
    }

    /// <summary>
    /// 冻结层数理论上限 4 层（由施加方控制；伤害公式用 Math.Max 兜底）
    /// </summary>
    public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 回合结束时全部消失
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && Amount > 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}
