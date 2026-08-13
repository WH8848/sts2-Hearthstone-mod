using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 力量光环（守护者艾格文）：持有者（随从）在场时，主人造成的攻击伤害 +Amount。
/// 挂在随从生物身上——随从死亡时该 Power 随生物移除，光环自动消失。
/// 艾格文死亡时通过 AegwynnLegacyPower 把此光环转移到下一张抽到的随从牌召唤的生物上。
/// </summary>
[RegisterPower]
public sealed class AegwynnAuraPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 主人的攻击伤害 +Amount（与 StrengthPower 同语义：仅 Powered 攻击）
    /// </summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer == null || Owner == null || Owner.PetOwner == null)
        {
            return 0m;
        }
        if (dealer != Owner.PetOwner.Creature)
        {
            return 0m;
        }
        if (!props.IsPoweredAttack())
        {
            return 0m;
        }
        return Amount;
    }
}
