using System.Collections.Generic;
using System.Threading.Tasks;
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
/// 寒冰屏障：当你将要承受致命伤害时，防止这些伤害，并在本回合中免疫。
/// 挂在吉安娜玩家身上（可见）。一次性：触发后进入免疫状态，本回合结束移除；
/// 未触发也于回合结束移除。
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
    /// 是否已触发（本回合免疫状态）
    /// </summary>
    private bool _immune;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 伤害修正：将要承受致命伤害时防止该伤害并进入免疫；
    /// 免疫状态下所有伤害归零（本回合）。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner || amount <= 0)
        {
            return 1m;
        }
        if (_immune)
        {
            // 本回合免疫：所有伤害无效
            return 0m;
        }
        // 致命判断：穿过格挡后的伤害 >= 当前生命
        decimal unblocked = amount - target.Block;
        if (unblocked >= target.CurrentHp)
        {
            // 防止致命伤害并进入免疫
            _immune = true;
            return 0m;
        }
        return 1m;
    }

    /// <summary>
    /// 回合结束：屏障消失（无论是否触发）
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner.Side == side)
        {
            await PowerCmd.Remove(this);
        }
    }
}
