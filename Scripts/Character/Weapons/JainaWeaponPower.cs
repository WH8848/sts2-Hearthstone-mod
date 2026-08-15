using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Weapons;

/// <summary>
/// 武器能力（炉石传说式武器位，挂在玩家角色身上）。
/// - Amount = 当前耐久度：角色每用武器攻击一次，耐久度 -1；归零时能力消失。
/// - Attack = 武器攻击力：角色获得的攻击次数数值等同于武器攻击力。
/// - 顶替：打出第二张武器能力卡时，旧武器能力（含攻击力）被新武器完全替换。
/// - 攻击次数：角色每回合最多攻击一次（JainaWeaponAttackAction），切换武器不重置次数。
/// 可见（能力图标显示剩余耐久）。
/// </summary>
[RegisterPower]
public sealed class JainaWeaponPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_jaina_weapon_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    /// <summary>
    /// 武器攻击力（顶替时随武器卡更新）
    /// </summary>
    public int Attack { get; private set; }

    /// <summary>
    /// 设置武器攻击力（挂载前调用）
    /// </summary>
    public void SetWeaponStats(int attack)
    {
        AssertMutable();
        Attack = attack;
    }

    /// <summary>
    /// 描述：动态注入武器攻击力变量 {Attack}（耐久度由游戏自动注入 {Amount}）。
    /// 注意：不使用 smartDescription（该路径非 virtual 无法注入自定义变量）。
    /// </summary>
    public override LocString Description
    {
        get
        {
            var loc = new LocString("powers", base.Id.Entry + ".description");
            loc.Add("Attack", Attack);
            return loc;
        }
    }

    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 单层能力：打出新武器时直接替换（顶替）
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 可见：能力栏显示武器图标与剩余耐久
    /// </summary>
    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 玩家回合开始：耐久已耗尽时兜底移除（正常流程在攻击后即移除）。
    /// 攻击行动点是角色每回合固有的（JainaWeaponAttackAction 自己每回合重置），与武器无关。
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Combat.CombatSide side, IReadOnlyList<Creature> participants,
        MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        var player = Owner?.Player;
        if (player == null || side != MegaCrit.Sts2.Core.Combat.CombatSide.Player)
        {
            return;
        }
        if (Amount <= 0)
        {
            // 耐久已耗尽（理论上挂载时即移除，这里兜底）
            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 战斗结束：武器不保留到下一场（Power 随战斗清理）
    /// </summary>
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        await Task.CompletedTask;
    }
}
