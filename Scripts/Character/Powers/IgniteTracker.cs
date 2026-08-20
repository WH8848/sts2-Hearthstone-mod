using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Keywords;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 引燃（Ignite）跟踪器：被附加引燃的卡牌会在 3 回合后消耗。
/// - 附加回合算第 1 回合（ApplyIgnite 记录当前玩家回合数）；
/// - 第 3 回合结束时（当前回合 ≥ 附加回合 + 2）若该卡还在<b>手牌</b>则被消耗
///   （打出/移出战斗的卡不再检查——只要不在手牌就不消耗）；
/// - 每玩家回合结束经 <see cref="IgniteClockPower.BeforeSideTurnEnd"/> 检查
///   该玩家手牌中带引燃关键词的卡（联机：两端同步执行同一命令，确定性一致）。
/// 附加方式：<see cref="ApplyIgnite"/>（给卡实例 AddKeyword + 记录附加回合）。
/// </summary>
public static class IgniteTracker
{
    /// <summary>
    /// 卡实例 → 附加引燃时的玩家回合数（战斗结束由 Entry 清理；卡实例随战斗回收）。
    /// </summary>
    private static readonly Dictionary<CardModel, int> IgnitedAtTurn = new();

    /// <summary>
    /// 给一张卡附加引燃（追加关键词标记 + 记录附加回合）。
    /// 附加回合算第 1 回合：第 3 回合结束（当前回合 ≥ 附加回合 + 2）时若还在手牌则消耗。
    /// </summary>
    public static void ApplyIgnite(CardModel card, Player player)
    {
        if (card == null || !card.IsMutable)
        {
            return;
        }
        // 关键词标记（卡面显示引燃图标；打出后关键词随实例保留，但检查只针对手牌）
        try
        {
            card.AddKeyword(JainaKeywords.Ignite);
        }
        catch
        {
            // 标记失败（不可变/已移除）不影响其余流程
        }
        int turn = player?.PlayerCombatState?.TurnNumber ?? 0;
        IgnitedAtTurn[card] = turn;
    }

    /// <summary>
    /// 该卡是否带引燃关键词（手牌/悬停显示用；也可用于效果判定）
    /// </summary>
    public static bool HasIgnite(CardModel card)
    {
        return card != null && card.Keywords.Contains(JainaKeywords.Ignite);
    }

    /// <summary>
    /// 玩家回合结束时检查该玩家手牌：带引燃且当前回合 ≥ 附加回合 + 2 的卡消耗。
    /// 只在手牌中检查（打出/移出战斗不消耗）；消耗后移除引燃记录。
    /// </summary>
    public static async Task TickPlayerTurnEnd(PlayerChoiceContext choiceContext, Player player)
    {
        var hand = player?.PlayerCombatState?.Hand;
        if (hand == null)
        {
            return;
        }
        int currentTurn = player.PlayerCombatState.TurnNumber;
        var toExhaust = new List<CardModel>();
        foreach (var card in hand.Cards)
        {
            if (card == null || !card.Keywords.Contains(JainaKeywords.Ignite))
            {
                continue;
            }
            if (IgnitedAtTurn.TryGetValue(card, out var ignitedTurn) &&
                currentTurn >= ignitedTurn + 2)
            {
                toExhaust.Add(card);
            }
        }
        foreach (var card in toExhaust)
        {
            // 消耗：进消耗堆（带引燃的卡消耗后不再回手）
            await CardCmd.Exhaust(choiceContext, card, causedByEthereal: false);
            IgnitedAtTurn.Remove(card);
        }
    }

    /// <summary>
    /// 战斗结束清理引燃记录（防止跨战斗残留——卡实例若跨战斗复用会误消耗）
    /// </summary>
    public static void Clear()
    {
        IgnitedAtTurn.Clear();
    }
}

/// <summary>
/// 引燃时钟：挂在玩家身上，玩家回合结束时触发 <see cref="IgniteTracker.TickPlayerTurnEnd"/>。
/// 隐藏（不可见，无图标）。战斗开始由 Entry 对每个玩家幂等挂载。
/// </summary>
[STS2RitsuLib.Interop.AutoRegistration.RegisterPower]
public sealed class IgniteClockPower : PowerModel
{
    public override MegaCrit.Sts2.Core.Entities.Powers.PowerType Type =>
        MegaCrit.Sts2.Core.Entities.Powers.PowerType.Buff;

    public override MegaCrit.Sts2.Core.Entities.Powers.PowerStackType StackType =>
        MegaCrit.Sts2.Core.Entities.Powers.PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 幂等挂载引燃时钟（战斗开始对所有玩家调用；已有则不动）
    /// </summary>
    public static async Task EnsureAppliedAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player?.Creature == null || player.Creature.Powers.Any(p => p is IgniteClockPower))
        {
            return;
        }
        await PowerCmd.Apply<IgniteClockPower>(choiceContext, [player.Creature], 1m, player.Creature, null);
    }

    /// <summary>
    /// 玩家回合结束：检查该玩家手牌中带引燃的卡（3 回合后消耗）
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        var player = Owner?.Player;
        if (player == null || side != CombatSide.Player)
        {
            return;
        }
        await IgniteTracker.TickPlayerTurnEnd(choiceContext, player);
    }
}
