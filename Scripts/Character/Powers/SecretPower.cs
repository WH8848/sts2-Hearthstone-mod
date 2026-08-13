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
    /// 异议是否已拦截过一次（预览阶段的 ModifyDamage 调用也会触发拦截，
    /// 因此不能在 ModifyDamageAdditive 里移除 Power——等实际伤害结算后再移除）
    /// </summary>
    private bool _consumed;

    /// <summary>
    /// 异议：敌人造成的攻击伤害降为 0。
    /// 注意：敌人意图预览（每回合开始计算意图伤害）也会调用本钩子，
    /// 若在这里立即消耗 Power，实际攻击时拦截已失效——所以只标记，
    /// 待 AfterDamageReceived（实际伤害结算）后再移除。
    /// </summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] SecretPower ModifyDamageAdditive: counterspell={IsCounterspell} amount={amount} dealer={dealer?.LogName ?? "null"} dealerSide={dealer?.Side} myAmount={Amount} consumed={_consumed}");
        if (!IsCounterspell && dealer != null && dealer.Side == CombatSide.Enemy && amount > 0 && Amount > 0)
        {
            _consumed = true;
            return -amount;
        }
        return 0m;
    }

    /// <summary>
    /// 异议实际拦截生效后：敌人伤害结算（含被拦为 0 的伤害）后移除 Power
    /// </summary>
    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (_consumed && dealer != null && dealer.Side == CombatSide.Enemy)
        {
            _ = PowerCmd.Remove(this);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 玩家回合开始：兜底清理（若敌人始终未攻击，防止残留）
    /// </summary>
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            _ = PowerCmd.Remove(this);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 法术反制：敌人对玩家施加的减益数量改为 0（拦截非攻击意图），触发一次后移除
    /// </summary>
    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] SecretPower TryModifyPowerAmountReceived: counterspell={IsCounterspell} power={canonicalPower?.Id.Entry ?? "null"} amount={amount} applier={applier?.LogName ?? "null"} applierSide={applier?.Side} targetSide={target.Side} myAmount={Amount}");
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
