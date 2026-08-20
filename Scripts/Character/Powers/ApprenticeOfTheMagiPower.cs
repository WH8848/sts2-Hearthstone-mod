using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 魔导师学徒：每回合开始，你的第一张奥术法术耗费减少1点。
/// 挂在吉安娜玩家身上（魔导师学徒遗物战斗开始施加，幂等）。
/// 参考卡雷苟斯（KalecgosPower）的"每回合第一张减费"模式：
/// - TryModifyEnergyCostInCombat：本回合尚未使用奥术法术时，玩家第一张奥术法术 -1 费；
/// - AfterCardPlayed：打出奥术法术（玩家手打，排除随机释放）后消耗本回合名额；
/// - BeforeSideTurnStart：新回合重置。
/// </summary>
[RegisterPower]
public sealed class ApprenticeOfTheMagiPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_apprentice_of_the_magi_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 本回合是否已使用奥术法术（名额已消耗）
    /// </summary>
    private bool _usedThisTurn;

    /// <summary>
    /// 幂等挂载（魔导师学徒遗物每场战斗开始调用；已有则不动）
    /// </summary>
    public static async Task EnsureAppliedAsync(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player?.Creature == null || player.Creature.Powers.Any(p => p is ApprenticeOfTheMagiPower))
        {
            return;
        }
        await PowerCmd.Apply<ApprenticeOfTheMagiPower>(choiceContext, [player.Creature], 1m, player.Creature, null);
    }

    /// <summary>
    /// 本回合尚未使用奥术法术时，玩家的第一张奥术法术 -1 费（最低 0）。
    /// 奥术法术判定：法术牌（攻击/技能，或带"法术牌"关键词的能力牌）+ 奥术派系
    /// （GetSchoolOf 动态判定，升级后派系变化的形态自动跟随）。
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (_usedThisTurn)
        {
            return false;
        }
        var player = Owner?.Player;
        if (player == null || card?.Owner != player)
        {
            return false;
        }
        if (!IsArcaneSpell(card))
        {
            return false;
        }
        modifiedCost = System.Math.Max(0m, originalCost - 1m);
        return modifiedCost != originalCost;
    }

    /// <summary>
    /// 打出奥术法术（玩家手打，排除随机释放 AutoPlay）后消耗本回合名额
    /// </summary>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_usedThisTurn && cardPlay.Card?.Owner == Owner?.Player &&
            IsArcaneSpell(cardPlay.Card) && !cardPlay.IsAutoPlay)
        {
            _usedThisTurn = true;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 玩家回合开始：重置名额
    /// </summary>
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            _usedThisTurn = false;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 奥术法术：法术牌（IsSpellCard：攻击/技能，或带"法术牌"关键词的能力牌）
    /// + 奥术派系（GetSchoolOf 动态判定）
    /// </summary>
    private static bool IsArcaneSpell(CardModel card)
    {
        if (card == null || !jaina.Scripts.Character.JainaCastTracker.IsSpellCard(card))
        {
            return false;
        }
        return jaina.Scripts.Character.JainaCastTracker.GetSchoolOf(card) == jaina.Scripts.Character.JainaSpellSchool.Arcane;
    }
}
