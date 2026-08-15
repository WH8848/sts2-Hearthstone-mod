using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using MinionLib.Action;
using MinionLib.Commands;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Weapons;

/// <summary>
/// 武器攻击行动点（MinionLib 手动攻击模式）：
/// 玩家点击自己的角色 → 选择一名敌人 → 角色用武器攻击，造成等同于武器攻击力的伤害。
/// - 行动点是角色每回合固有的 1 点（与武器无关）：常驻不随回合结束移除，
///   每回合开始时自动重置为 1（BeforeSideTurnStart）；
/// - 武器只赋予攻击力：攻击力为 0（未装备武器）时不可行动（CanAct 检查）；
/// - 每次攻击后武器耐久度 -1，归零时武器能力消失（见 JainaWeaponSlot.ConsumeDurability）。
/// </summary>
[RegisterPower]
public sealed class JainaWeaponAttackAction : ActionModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_jaina_weapon_attack_action.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override TargetType TargetType => TargetType.AnyEnemy;

    /// <summary>
    /// 行动点是角色固有的：常驻（不随回合结束移除），每回合开始重置为 1
    /// </summary>
    public override bool AutoRemoveAtTurnEnd => false;

    /// <summary>
    /// 每次攻击后消耗 1 点行动次数
    /// </summary>
    public override bool DecrementAfterAct => true;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 角色每回合拥有 1 点攻击行动点（与武器无关，武器只赋予攻击力）。
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        var player = Owner?.Player;
        if (player == null || side != CombatSide.Player)
        {
            return;
        }
        // 重置为 1 点（攻击后为 0，下回合恢复）
        if (Amount != 1m)
        {
            SetAmount(1);
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// 不可行动条件：行动点用完（Amount=0）或攻击力为 0（未装备武器）。
    /// 攻击力来自当前武器能力 JainaWeaponPower.Attack。
    /// </summary>
    public override bool CanAct(ICombatState combatState)
    {
        if (!base.CanAct(combatState))
        {
            return false;
        }
        var weapon = Owner?.Powers.OfType<JainaWeaponPower>().FirstOrDefault();
        return weapon != null && weapon.Attack > 0 && weapon.Amount > 0;
    }

    protected override async Task OnAct(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (target == null || !Owner.IsAlive)
        {
            return;
        }

        // 读取当前武器能力（攻击力/耐久）；没有武器则无法攻击
        var weapon = Owner.Powers.OfType<JainaWeaponPower>().FirstOrDefault();
        if (weapon == null || weapon.Attack <= 0 || weapon.Amount <= 0)
        {
            return;
        }
        var attack = weapon.Attack;

        // 角色冲撞攻击：Move 标记触发荆棘反伤与振翅（IsPoweredAttack）
        await MinionAnimCmd.PlayBumpAttackAsync(Owner, target,
            () => CreatureCmd.Damage(choiceContext, [target], attack, ValueProp.Move, Owner));

        // 每攻击一次，武器耐久度 -1；归零时武器能力消失
        await JainaWeaponSlot.ConsumeDurability(choiceContext, Owner, weapon);
    }
}
