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
/// 寒冰屏障的免疫状态：致命伤害被防止后获得[gold]免疫[/gold]（免疫一切伤害,伤害全归零），
/// 同时<b>锁定触发时的当前生命值</b>——免疫期间任何结算后 HP 不低于锁定值;
/// 持续到你的<b>下回合开始</b>（玩家下回合开始时免疫结束）。
/// </summary>
[RegisterPower]
public sealed class IceBlockImmunityPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_ice_block_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 可见：显示免疫状态图标
    /// </summary>
    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 触发冰山屏障时的当前生命值（锁血值;0 = 未绑定）
    /// </summary>
    public decimal LockedHp;

    /// <summary>
    /// 免疫：所有伤害归零
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner || amount <= 0)
        {
            return 1m;
        }
        return 0m;
    }

    /// <summary>
    /// 锁血：任何伤害结算后,当前生命不低于触发时的锁定值（双保险——
    /// 免疫已让伤害归零;此处兜底非伤害/结算差异路径）
    /// </summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || LockedHp <= 0m || Owner.CurrentHp >= LockedHp)
        {
            return;
        }
        await CreatureCmd.Heal(Owner, LockedHp - Owner.CurrentHp);
    }

    /// <summary>
    /// 你的回合开始：免疫结束（持续到你的下回合开始）
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner.Side == side)
        {
            await PowerCmd.Remove(this);
        }
    }
}
