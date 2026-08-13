using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 寒冰护盾：受到攻击时（伤害结算前），获得 Amount 点护甲。回合结束时移除。
/// 用 BeforeDamageReceived：护甲在本次伤害结算前获得，可挡住当次攻击。
/// </summary>
[RegisterPower]
public sealed class IceBarrierPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 玩家受到攻击时（结算前），获得 Amount 点护甲
    /// </summary>
    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && amount > 0 && Amount > 0)
        {
            await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Move, null);
        }
    }

    /// <summary>
    /// 回合结束时移除
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && Amount > 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}
