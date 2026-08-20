using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 奥术增幅（Arcane Amplifier）：你的英雄技能会额外造成 2 点伤害。
/// 挂在吉安娜玩家身上（Amount=2），由奥术增幅体遗物在每场战斗开始时施加；
/// 与野火（WildfirePower）同机制，各英雄技能卡读取 Amount 作为额外伤害。
/// 可见（图标显示加成）。
/// </summary>
[RegisterPower]
public sealed class ArcaneAmplifierPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_arcane_amplifier_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 英雄技能额外伤害（固定 2 点）
    /// </summary>
    public int AmplifierBonus => (int)Amount;

    /// <summary>
    /// 幂等挂载奥术增幅（遗物每场战斗开始调用；已有则不动）
    /// </summary>
    public static async Task EnsureAppliedAsync(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player?.Creature == null || player.Creature.Powers.Any(p => p is ArcaneAmplifierPower))
        {
            return;
        }
        await PowerCmd.Apply<ArcaneAmplifierPower>(choiceContext, [player.Creature], 2m, player.Creature, null);
    }
}
