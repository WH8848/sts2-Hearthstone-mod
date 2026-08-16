using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 地标冷却（挂在<b>地标实体</b>上）。
/// - Amount = 剩余冷却回合数：玩家回合开始时 -1，归零时能力消失（地标恢复可用）。
/// - 每次使用地标后挂 2 层（每两个回合可点击使用一次：使用回合后的下一回合不可用，再下一回合恢复）。
/// - 打出地标当回合即可使用（不挂冷却，见 <see cref="Minions.JainaLandmarkBase.OnSummon"/>）。
/// 冷却期间不授予使用行动点（<see cref="Minions.JainaLandmarkBase"/> 处理）。
/// </summary>
[RegisterPower]
public sealed class LandmarkCooldownPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_landmark_cooldown.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 可见：能力栏显示冷却图标与剩余回合数
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
}
