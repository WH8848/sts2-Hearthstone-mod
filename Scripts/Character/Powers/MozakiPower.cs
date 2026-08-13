using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 决斗大师莫扎奇的力量光环：每施放一张攻击/技能牌，本 Power 层数 +1；
/// 持有者（随从）在场期间，主人攻击伤害 +Amount。
/// 挂在随从生物身上——随从死亡时该 Power 随生物移除，累计的力量加成全部消失。
/// </summary>
[RegisterPower]
public sealed class MozakiPower : PowerModel
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
