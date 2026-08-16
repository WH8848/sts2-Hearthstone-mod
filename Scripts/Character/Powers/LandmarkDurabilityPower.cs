using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 地标耐久度（挂在<b>地标实体</b>上）。
/// - Amount = 当前耐久度：每次使用地标效果后 -1；
/// - 归零时地标被摧毁（移除战场，见 <see cref="Minions.JainaLandmarkBase"/>）；
/// - 兼作地标伤害免疫：地标生命值视觉 = 剩余耐久度，生命值只在使用时同步减少，
///   不受任何伤害影响（ModifyDamageMultiplicative 返回 0）。
/// </summary>
[RegisterPower]
public sealed class LandmarkDurabilityPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_landmark_durability.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 可见：能力栏显示耐久图标与剩余耐久
    /// </summary>
    protected override bool IsVisibleInternal => true;

    public override LocString Description
    {
        get
        {
            var loc = new LocString("powers", base.Id.Entry + ".description");
            loc.Add("Amount", (int)Amount);
            return loc;
        }
    }

    /// <summary>
    /// 地标免疫所有伤害（生命值视觉 = 耐久度，只在使用地标时同步减少）。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner)
        {
            return 1m;
        }
        return 0m;
    }
}
