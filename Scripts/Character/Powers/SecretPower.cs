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
/// 奥秘：
/// - 异议（IsCounterspell=false）：拦截敌人下一次造成的攻击伤害（降为 0），触发后移除。
/// - 法术反制（IsCounterspell=true）：拦截敌人下一次施加的减益（非攻击意图），触发后移除。
/// </summary>
[RegisterPower]
public sealed class SecretPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// true = 法术反制（拦截减益），false = 异议（拦截攻击伤害）
    /// </summary>
    public bool IsCounterspell;

    /// <summary>
    /// 异议：敌人造成的攻击伤害降为 0，触发一次后移除
    /// </summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!IsCounterspell && dealer != null && dealer.Side == CombatSide.Enemy && amount > 0 && Amount > 0)
        {
            _ = PowerCmd.Decrement(this);
            return -amount;
        }
        return 0m;
    }

    /// <summary>
    /// 法术反制：敌人对玩家施加的减益数量改为 0（拦截非攻击意图），触发一次后移除
    /// </summary>
    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (!IsCounterspell || Amount <= 0)
        {
            return false;
        }
        if (applier == null || applier.Side != CombatSide.Enemy || target.Side != CombatSide.Player)
        {
            return false;
        }
        if (canonicalPower.Type != PowerType.Debuff)
        {
            return false;
        }
        modifiedAmount = 0m;
        _ = PowerCmd.Decrement(this);
        return true;
    }
}
