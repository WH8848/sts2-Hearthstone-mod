using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 艾格文继承效果（挂在继承随从身上，可见）。
/// 艾格文亡语：下一张抽到的随从牌继承"力量+2 与亡语"——
/// 继承随从打出后获得 +2 力量，并挂上本 Power：
/// 该随从死亡时移除 +2 力量，并把 AegwynnLegacyPower 继续传递给下一张抽到的随从牌
/// （链式传递，炉石原版语义）。能力图标显示在随从身上。
/// </summary>
[RegisterPower]
public sealed class AegwynnInheritedPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_aegwynn_inherited_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 继承随从死亡：返还 2*层数 力量，并将继承效果（同层数）传给下一张抽到的随从牌
    /// </summary>
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature != Owner)
        {
            return;
        }
        var petOwner = Owner.PetOwner;
        if (petOwner == null)
        {
            return;
        }
        int count = System.Math.Max(1, (int)Amount);
        await PowerCmd.Apply<StrengthPower>(choiceContext, [petOwner.Creature], -2m * count, Owner, null);
        await PowerCmd.Apply<AegwynnLegacyPower>(choiceContext, [petOwner.Creature], count, Owner, null);
    }
}
