using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 冻结：被冻结的角色攻击造成的伤害减少12.5%，可叠加。
/// 最大可叠加8层，回合结束全部消失。
/// 随从给予的冻结不会被人工制品阻挡（滑冰元素/瓦尔登·晨拥，见 ArtifactFreezeBypassPatch）。
/// </summary>
[RegisterPower]
public sealed class FreezePower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_freeze_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    /// <summary>
    /// 最大冻结层数
    /// </summary>
    public const int MaxStacks = 8;

    /// <summary>
    /// 下一次施加冻结时无视人工制品（ArtifactPower）阻挡。
    /// 由滑冰元素/瓦尔登·晨拥在施加前置位、施加后清除（try/finally），
    /// 用于实现"冻结不被人工制品阻挡"（见 ArtifactFreezeBypassPatch）。
    /// 联机：两端各自在战吼中置位/清除（命令确定性执行），行为一致。
    /// </summary>
    internal static bool BypassArtifactNextApply;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 冻结者攻击时，其造成的伤害减少 12.5% × 层数（最多 8 层 = 减 100%）
    /// </summary>
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner)
        {
            return 1m;
        }
        if (!props.IsPoweredAttack())
        {
            return 1m;
        }
        return Math.Max(0m, 1m - 0.125m * Amount);
    }

    /// <summary>
    /// 冻结层数理论上限 8 层（由施加方控制；伤害公式用 Math.Max 兜底）
    /// </summary>
    public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[JainaFreeze] apply target={target.Name}(side={target.Side}) amount={amount} existing={Amount} card={(cardSource == null ? "null" : cardSource.Id.Entry)}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 施加完成：打印最终层数（若为 0 = 被抵挡/免疫）
    /// </summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[JainaFreeze] applied result owner={Owner?.Name}(side={Owner?.Side}) stacks={Amount} by={cardSource?.Id.Entry}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 回合结束时全部消失
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side && Amount > 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}
