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
using MegaCrit.Sts2.Core.Saves.Runs;
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
    /// 武器攻击力（顶替时随武器卡更新）。
    /// [SavedProperty]：联机状态同步/战斗存档读档会重建 Power 实例，
    /// 普通属性不参与序列化、重建后丢失为 0——武器攻击行动
    /// （JainaWeaponAttackAction.CanAct 判定 Attack &gt; 0）会因此失效，
    /// 表现为"装备武器后点击自己无法攻击敌人"。
    /// </summary>
    [SavedProperty]
    public int Attack { get; private set; }

    /// <summary>
    /// 武器摧毁时回调（炉石武器亡语：耐久归零或被新武器顶替时触发）。
    /// 由武器能力卡 OnPlay 挂载；无则跳过。
    /// </summary>
    public Func<PlayerChoiceContext, Task>? OnDestroyed { get; set; }

    /// <summary>
    /// 武器攻击后回调（角色用武器攻击一次后触发）。
    /// 由武器能力卡 OnPlay 挂载；无则跳过。
    /// </summary>
    public Func<PlayerChoiceContext, Task>? OnAttack { get; set; }

    /// <summary>
    /// 设置武器攻击力（挂载前调用）
    /// </summary>
    public void SetWeaponStats(int attack)
    {
        AssertMutable();
        Attack = attack;
    }

    /// <summary>
    /// 武器特殊效果描述键（powers 表完整键，如 "JAINA_POWER_ALUNETH_EFFECT.description"）。
    /// 由武器能力卡装备时设置；能力栏悬停只显示特殊效果（攻击力显示在角色攻击意图，
    /// 耐久度显示在能力图标右下角标——都不出现在能力栏）。
    /// </summary>
    public string? EffectLocKey { get; set; }

    /// <summary>
    /// 描述：有特殊效果时只显示特殊效果文本（攻击力/耐久度不显示在能力栏）；
    /// 无特殊效果时显示基础"武器"说明（同样不含攻击力/耐久度数字）。
    /// </summary>
    public override LocString Description
    {
        get
        {
            if (EffectLocKey != null)
            {
                return new LocString("powers", EffectLocKey);
            }
            return new LocString("powers", base.Id.Entry + ".description");
        }
    }

    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 单层能力：打出新武器时直接替换（顶替）
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 可见：能力栏显示武器图标
    /// </summary>
    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 能力图标右下角标：显示武器耐久度（Amount）。
    /// 攻击力显示在角色攻击意图（PlayerAttackIntentPatch），耐久度显示在此角标——
    /// 两者都不出现在能力栏描述（只显示特殊效果）。
    /// </summary>
    public IReadOnlyList<STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels.ExtraIconAmountLabelSlot>
        GetPowerExtraIconAmountLabelSlots()
    {
        return
        [
            STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels.ExtraIconAmountLabelSlot.At(
                STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels.ExtraIconAmountLabelCorner.BottomRight,
                ((int)Amount).ToString())
        ];
    }

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
            // 兜底移除同样触发武器亡语（OnDestroyed 回调）
            if (OnDestroyed != null)
            {
                await OnDestroyed(choiceContext);
            }
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
