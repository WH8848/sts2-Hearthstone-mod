using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 野火：你的英雄技能造成的伤害增加 Amount 点（本局对战永久，可叠加）。
/// 挂在吉安娜玩家身上，由野火卡打出时施加。可见（图标显示加成层数）。
/// </summary>
[RegisterPower]
public sealed class WildfirePower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_wildfire_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 英雄技能伤害加成层数（由火焰冲击等英雄技能卡读取）
    /// </summary>
    public int WildfireStacks => (int)Amount;
}
