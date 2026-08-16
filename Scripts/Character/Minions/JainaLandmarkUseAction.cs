using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MinionLib.Action;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 地标的使用行动点（MinionLib ActionModel 行动系统）。
/// 玩家点击地标 → 选择一名角色（任意活物：自己/己方随从/敌人）→ 触发地标效果。
/// - 仅当地标不在冷却中时由 <see cref="JainaLandmarkBase"/> 在玩家回合开始时授予；
/// - Amount = 本回合可使用次数（1 次）；每次使用后自动消耗并消失（使用后进入冷却）。
/// </summary>
[RegisterPower]
public sealed class JainaLandmarkUseAction : ActionModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_landmark_use.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    /// <summary>
    /// 目标：任意活物（给予一名角色 1 层冻结）
    /// </summary>
    public override TargetType TargetType => Cards.JainaTargetTypes.AnyTargetable;

    /// <summary>
    /// 使用行动点是当回合有效的，回合结束自动移除（冷却中不授予）
    /// </summary>
    public override bool AutoRemoveAtTurnEnd => true;

    /// <summary>
    /// 每次使用后消耗 1 点（归零自动移除；之后地标进入冷却）
    /// </summary>
    public override bool DecrementAfterAct => true;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override async Task OnAct(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (target == null || !Owner.IsAlive)
        {
            return;
        }
        var landmark = Owner.Monster as JainaLandmarkBase;
        if (landmark == null)
        {
            return;
        }
        await landmark.PerformUse(choiceContext, target);
    }
}
