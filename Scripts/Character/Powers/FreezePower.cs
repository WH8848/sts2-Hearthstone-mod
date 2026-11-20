using System;
using System.Collections.Generic;
using System.Threading;
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
/// 冻结：每层使被冻结的角色攻击造成的伤害减少12.5%，最多减少 25%。
/// 回合结束减少 2 层。
/// 随从给予的冻结不会被人工制品阻挡。
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

    private static readonly AsyncLocal<Creature?> ArtifactBypassTarget = new();

    internal static bool BypassesArtifact(Creature target) => ReferenceEquals(ArtifactBypassTarget.Value, target);

    internal static async Task ApplyIgnoringArtifact(
        PlayerChoiceContext choiceContext, Creature target, decimal amount, Creature applier, CardModel? cardSource)
    {
        ArtifactBypassTarget.Value = target;
        await PowerCmd.Apply<FreezePower>(choiceContext, target, amount, applier, cardSource);
    }

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 冻结者攻击时，其造成的伤害减少 12.5% × 层数，最多减少 25%
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
        var multiplier = Math.Max(0.75m, 1m - 0.125m * Amount);
        return multiplier;
    }

    /// <summary>
    /// 冻结层数理论上限 8 层（由施加方控制；伤害公式用 Math.Max 兜底）
    /// </summary>
    public override Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 施加完成：打印最终层数（若为 0 = 被抵挡/免疫）
    /// </summary>
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 层数变化（叠层路径）：PowerCmd.Apply 对已有实例走 ModifyAmount，不触发 AfterApplied，
    /// 这里补日志，保证两端每一层变化都有记录可对比。
    /// </summary>
    /// <summary>
    /// 回合结束时减少 2 层（不足 2 层则全部消失）
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side != side || Amount <= 0)
        {
            return;
        }
        int next = Amount - 2;
        if (next <= 0)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            SetAmount(next);
        }
    }
}
