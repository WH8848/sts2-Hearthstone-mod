using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Weapons;

/// <summary>
/// 艾露尼斯之赐：装备艾露尼斯期间，每回合开始时抽三张牌。
/// 挂在玩家身上（可见）；打出其他武器时由 <see cref="AlunethCard"/> 顶替移除。
/// </summary>
[RegisterPower]
public sealed class AlunethPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_aluneth_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 每回合开始时抽三张牌
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side,
        System.Collections.Generic.IReadOnlyList<Creature> participants, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        var player = Owner?.Player;
        if (player == null || side != MegaCrit.Sts2.Core.Combat.CombatSide.Player)
        {
            return;
        }
        await CardPileCmd.Draw(choiceContext, 3, player);
    }
}
