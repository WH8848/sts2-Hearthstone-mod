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
/// 法术反制：敌人的下一个非攻击意图不会触发任何效果（只拦非攻击，留下攻击意图）。
/// 拦截范围：敌人施加的任何 Power（Buff 增益自身 / Debuff 减益玩家 / CardDebuff 类）
/// 与敌人获得的格挡（Defend）。攻击意图的伤害不拦。
/// 与异议（ObjectionPower）相互独立——两者同时在场时各自拦截一类意图。
/// </summary>
[RegisterPower]
public sealed class CounterspellPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_counterspell_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 敌人施加的任何 Power（增益/减益）数量改为 0
    /// </summary>
    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (Amount <= 0)
        {
            return false;
        }
        if (applier == null || applier.Side != CombatSide.Enemy)
        {
            return false;
        }
        modifiedAmount = 0m;
        return true;
    }

    /// <summary>
    /// 敌人获得的格挡改为 0（Defend 意图）
    /// </summary>
    public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (Amount > 0 && target.Side == CombatSide.Enemy && block > 0)
        {
            return -block;
        }
        return 0m;
    }

    /// <summary>
    /// 玩家回合开始：移除（拦截持续到本回合结束；与异议可各自独立生效）
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
