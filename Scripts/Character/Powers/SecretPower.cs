using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 奥秘（异议/法术反制）：拦截敌人下一次造成的伤害（降为 0），触发后移除。
/// 简化实现：异议与法术反制共用此 Power（拦截一次任意来源的伤害）。
/// </summary>
[RegisterPower]
public sealed class SecretPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 敌人造成的伤害降为 0，触发一次后移除
    /// </summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != null && dealer.Side == CombatSide.Enemy && amount > 0 && Amount > 0)
        {
            _ = PowerCmd.Decrement(this);
            return -amount;
        }
        return 0m;
    }
}
