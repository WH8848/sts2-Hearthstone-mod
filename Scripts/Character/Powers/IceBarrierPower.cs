using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 寒冰护盾：本回合内受到攻击时（伤害结算前），获得 Amount 点护甲，随后本 Power 消失。
/// 用 BeforeDamageReceived：护甲在本次伤害结算前获得，可挡住当次攻击。
/// 已触发过即消失（本回合内最多挡一次）；若本回合内未被攻击，下个玩家回合开始兜底移除。
/// </summary>
[RegisterPower]
public sealed class IceBarrierPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_ice_barrier_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 吉安娜或其随从受到攻击时（伤害结算前），获得 Amount 点护甲（动态 BlockVar，
    /// 吃敏捷加成，与卡面 {Block:diff()} 显示一致），然后本 Power 消失。
    /// 随从受击的伤害会转移到主人的护甲（DamageCmd 内 PetOwner 转移），
    /// 因此随从被攻击时同样触发。
    /// </summary>
    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // target 是自己，或是自己的随从（随从受击伤害转入主人护甲）
        bool isOwnerOrPet = target == Owner || target.PetOwner?.Creature == Owner;
        if (isOwnerOrPet && amount > 0 && Amount > 0)
        {
            await CreatureCmd.GainBlock(Owner, new BlockVar(Amount, ValueProp.Move), null);
            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 下一个玩家回合开始时移除。
    /// 打出后覆盖整个敌方回合的攻击窗口（不能在玩家回合结束就移除）。
    /// </summary>
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && Amount > 0)
        {
            _ = PowerCmd.Remove(this);
        }
        return Task.CompletedTask;
    }
}
