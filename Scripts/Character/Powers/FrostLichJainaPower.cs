using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 元素吸血（冰霜女巫吉安娜光环）：
/// 在本局对战中，你的所有元素随从造成伤害时回复你等量生命。
/// 挂在吉安娜身上（Amount=1 表示激活），元素随从在 JainaMinionBase.AfterDamageGiven
/// 中检查本光环后执行吸血。
/// </summary>
[RegisterPower]
public sealed class FrostLichJainaPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_frost_lich_jaina_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}
