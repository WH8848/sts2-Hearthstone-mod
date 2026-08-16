using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 观星重放（Stargazing Replay）：观星抽到的奥术法术牌本回合内具有重放1——
/// 该牌打出时自动重放一次（施放两次），用一次即消耗；回合结束未用也移除。
/// 参考游戏原版 DuplicationPower 的 ModifyCardPlayCount 机制。
/// </summary>
[RegisterPower]
public sealed class StargazingReplayPower : PowerModel, IModPowerAssetOverrides
{
    /// <summary>
    /// 目标卡（观星抽到的那张奥术法术牌）
    /// </summary>
    public CardModel? Target;

    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_stargazing_replay_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 目标卡打出时施放两次（重放 1）
    /// </summary>
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card != Target || card.Owner.Creature != Owner)
        {
            return playCount;
        }
        return playCount + 1;
    }

    /// <summary>
    /// 用一次即消耗（牌已打出，重放完成）
    /// </summary>
    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (card == Target)
        {
            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 回合结束未打出也移除
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.Side == side)
        {
            await PowerCmd.Remove(this);
        }
    }
}
