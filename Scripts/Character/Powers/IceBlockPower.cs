using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
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
/// 寒冰屏障（常驻可叠层能力，挂在吉安娜玩家身上，可见，Amount = 剩余层数）。
/// - 常驻：不随回合结束消失；多次打出可叠加层数；
/// - 当将要承受<b>致命伤害</b>时：移除 1 层，此次致命伤害变为 0，
///   并挂载 <see cref="IceBlockImmunityPower"/>（免疫直到下回合开始）；
/// - 层数归零时本 Power 移除（屏障耗尽）。
/// </summary>
[RegisterPower]
public sealed class IceBlockPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_ice_block_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    /// <summary>
    /// 本次伤害是否被屏障防住（在 AfterDamageReceived 中执行移除层数与免疫挂载）
    /// </summary>
    private bool _pendingTrigger;

    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 可叠层：多次打出寒冰屏障叠加层数
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 可见：能力栏显示屏障层数
    /// </summary>
    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 伤害修正：将要承受致命伤害时（穿过格挡的伤害 >= 当前生命）防止该伤害（归零），
    /// 标记触发（移除 1 层 + 免疫由 AfterDamageReceived 执行——本 hook 同步，不能异步）。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner || amount <= 0)
        {
            return 1m;
        }
        // 致命判断：穿过格挡后的伤害 >= 当前生命
        decimal unblocked = amount - target.Block;
        if (unblocked >= target.CurrentHp)
        {
            _pendingTrigger = true;
            // 联机诊断：两端触发不一致时（StateDivergence——一端屏障消耗+免疫、
            // 另一端未触发）对比 amount/hp/block，定位伤害或状态分歧
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[JainaDiag] IceBlock trigger: dealer={dealer?.Name ?? "null"} amount={amount} hp={target.CurrentHp} block={target.Block} unblocked={unblocked} stacks={Amount}");
            return 0m;
        }
        return 1m;
    }

    /// <summary>
    /// 伤害结算后：若本次伤害被屏障防住（伤害归零 → 目标存活，此 hook 正常触发），
    /// 移除 1 层（归零时屏障耗尽移除），并挂载免疫（直到下回合开始）。
    /// </summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target,
        DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || !_pendingTrigger)
        {
            return;
        }
        _pendingTrigger = false;
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[JainaDiag] IceBlock consumed: stacks={Amount} -> {(Amount <= 1 ? 0 : Amount - 1)} then immunity");

        // 移除 1 层；层数归零时屏障耗尽（本 Power 移除）
        if (Amount <= 1)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            await PowerCmd.Decrement(this);
        }

        // 免疫直到下回合开始
        await PowerCmd.Apply<IceBlockImmunityPower>(choiceContext, [Owner], 1m, Owner, null);
    }
}
