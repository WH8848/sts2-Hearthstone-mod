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
    /// 行动点是角色固有的：常驻（不随回合结束移除），每回合开始重置为 1。
    /// 注意：不能使用 DecrementAfterAct（框架会走 PowerCmd.Decrement，
    /// 在 Amount 归零时 ShouldRemoveDueToAmount 会自动移除本 Power）——
    /// 改为在 OnAct 末尾手动 SetAmount 扣点（SetAmount 不触发移除检查）。
    /// </summary>
    public override bool AutoRemoveAtTurnEnd => false;

    /// <summary>
    /// 每次攻击后消耗 1 点行动次数（手动扣除，见 OnAct；避免 Power 被自动移除）
    /// </summary>
    public override bool DecrementAfterAct => false;

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
        // 行动点恢复后刷新玩家攻击意图（可攻击时显示等同于攻击力的攻击意图）
        PlayerAttackIntentPatch.Refresh(Owner);
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

        // 手动扣除 1 点行动次数（不用 DecrementAfterAct：PowerCmd.Decrement 在 Amount 归零时
        // 会经 ShouldRemoveDueToAmount 自动移除本 Power，导致下回合行动点消失）
        if (Amount > 0)
        {
            SetAmount((int)Amount - 1);
        }

        // 攻击后刷新玩家攻击意图（行动点耗尽 → 意图消失）
        PlayerAttackIntentPatch.Refresh(Owner);

        // 每攻击一次，武器耐久度 -1；归零时武器能力消失
        await JainaWeaponSlot.ConsumeDurability(choiceContext, Owner, weapon);

        // 武器攻击后回调（如金属探测器升级形态：攻击1次，获取一张幸运币）
        if (weapon.OnAttack != null)
        {
            await weapon.OnAttack(choiceContext);
        }
    }
}
