using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 异议：敌人的下一个攻击意图不会造成任何伤害（只拦攻击，留下非攻击意图）。
/// 注意：敌人意图预览（每回合开始计算意图伤害）也会调用 ModifyDamageAdditive，
/// 若在预览时消耗 Power，实际攻击时拦截已失效——因此只标记，
/// 待 AfterDamageReceived（实际伤害结算）后再移除。
/// </summary>
[RegisterPower]
public sealed class ObjectionPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_objection_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 是否已拦截过一次（预览调用不消耗，实际结算后移除）
    /// </summary>
    private bool _consumed;

    /// <summary>
    /// 敌人造成的攻击伤害降为 0。
    /// 0.111.1 中 ModifyDamageAdditive 在意图预览（cardPlay == null）与实际结算
    /// （cardPlay != null）两个阶段都会调用；只在结算阶段标记消耗，
    /// 避免预览后任意其他敌人伤害结算就把拦截提前消耗掉。
    /// </summary>
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != null && dealer.Side == CombatSide.Enemy && amount > 0 && Amount > 0)
        {
            if (cardPlay != null)
            {
                _consumed = true;
            }
            return -amount;
        }
        return 0m;
    }

    /// <summary>
    /// 实际伤害结算后移除（含被拦为 0 的伤害）
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
    /// 玩家回合开始：兜底清理（若敌人始终未攻击）
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
}
