using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
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
    /// Power 栏显示值：格挡量（含敏捷加成）——寒冰护盾的层数语义就是格挡量，
    /// 与卡面 {Block:diff()} 一致（卡面吃敏捷显示 11 时，Power 栏不再固定显示 8）。
    /// </summary>
    public override int DisplayAmount
    {
        get
        {
            var dexterity = Owner?.GetPower<DexterityPower>();
            return (int)(Amount + (dexterity?.Amount ?? 0));
        }
    }

    /// <summary>
    /// 吉安娜或其随从受到攻击时（伤害结算前），获得 Amount 点护甲（动态 BlockVar，
    /// 吃敏捷加成，与卡面 {Block:diff()} 显示一致），然后本 Power 消失。
    /// 随从受击的伤害会转移到主人的护甲（DamageCmd 内 PetOwner 转移），
    /// 因此随从被攻击时同样触发。
    /// 仅"敌人造成的伤害"算受到攻击：吉安娜自己/己方效果对随从造成的伤害（死亡凋零、
    /// 火球打自己随从等）不触发。
    /// </summary>
    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // target 是自己，或是自己的随从（随从受击伤害转入主人护甲）
        bool isOwnerOrPet = target == Owner || target.PetOwner?.Creature == Owner;
        // 只有敌方造成的伤害才算"受到攻击"（炉石寒冰护盾语义）
        bool isEnemyDamage = dealer != null && dealer.Side != Owner.Side;
        if (isOwnerOrPet && isEnemyDamage && amount > 0 && Amount > 0)
        {
            await CreatureCmd.GainBlock(Owner, new BlockVar(Amount, ValueProp.Move), null);
            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 下一个玩家回合开始时移除。
    /// 打出后覆盖整个敌方回合的攻击窗口（不能在玩家回合结束就移除）。
    /// await 而非 fire-and-forget：避免移除的 AfterRemoved 钩子（push/pop 模型）
    /// 与当前钩子链交错导致"Tried to pop model"模型栈警告。
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && Amount > 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}
