using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 考达拉幼龙光环（挂在随从身上，随从死亡自动失效）：
/// - 你的英雄技能变为1费（战斗中费用解析固定为 1）；
/// - 你可以使用任意次数的英雄技能（手打的英雄技能卡打出后回到手牌；
///   自动打出（小精灵驾驭者等）不回手）。
/// </summary>
[RegisterPower]
public sealed class ColdarraDrakePower : PowerModel, IModPowerAssetOverrides
{
    private static readonly ConditionalWeakTable<CardPlay, object> RequestedReturns = new();

    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_coldarra_drake_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 你的英雄技能变为1费：战斗中费用解析时，主人（随从的主人）的英雄技能卡费用固定为 1。
    /// 注意：Power 挂在随从身上，Owner.Player 对随从为 null——必须用 Owner.PetOwner
    /// （随从的主人），否则 1 费与回手都不生效（实测：playerNull=True）。
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (Owner == null || !Owner.IsAlive)
        {
            return false;
        }
        var player = Owner.PetOwner;
        if (player == null || card.Owner != player)
        {
            return false;
        }
        // 只修改英雄技能卡（火焰冲击/二级火焰冲击/奥术爆裂/冰冷触摸等）
        if (!HeroPowerHandHelper.IsHeroPowerCard(card))
        {
            return false;
        }
        modifiedCost = 1;
        return true;
    }

    internal static Task ReturnHeroPowerPlayedThisTurn(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = player.Creature.CombatState;
        var play = CombatManager.Instance.History.CardPlaysFinished
            .LastOrDefault(entry => entry.HappenedThisTurn(combatState) && IsManualHeroPower(entry.CardPlay, player))
            ?.CardPlay;
        return play == null ? Task.CompletedTask : ReturnOrQueue(choiceContext, player, play);
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner is { IsAlive: true, PetOwner: { } player } && IsManualHeroPower(cardPlay, player))
        {
            return ReturnOrQueue(choiceContext, player, cardPlay);
        }
        return Task.CompletedTask;
    }

    private static bool IsManualHeroPower(CardPlay play, Player player) =>
        play.Player == player && !play.IsAutoPlay && play.PlayIndex == play.PlayCount - 1 &&
        HeroPowerHandHelper.IsHeroPowerCard(play.Card);

    private static Task ReturnOrQueue(PlayerChoiceContext choiceContext, Player player, CardPlay play)
    {
        var card = play.Card;
        if (card.Owner != player || card.Pile == null || RequestedReturns.TryGetValue(play, out _))
        {
            return Task.CompletedTask;
        }

        RequestedReturns.Add(play, new object());
        if (card.Pile.Type == PileType.Hand)
        {
            return Task.CompletedTask;
        }

        return JainaPendingRewardQueue.GrantOrQueue(choiceContext, player, _ =>
            card.Owner == player && card.Pile is { Type: not PileType.Hand }
                ? CardPileCmd.Add(card, PileType.Hand)
                : Task.CompletedTask);
    }
}
