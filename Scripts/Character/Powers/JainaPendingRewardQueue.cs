using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 任务奖励"等待手牌空位"队列（炉石语义）：
/// 手牌满时完成任务（禁忌序列→源生之石、打开时空之门→时空扭曲、任务线→奥术师晨拥/下阶段任务卡/抽法术）
/// 奖励<b>不丢失</b>——入队等待，手牌出现空位（打出卡/抽牌/入手后）自动发放。
/// 队列按玩家（ConditionalWeakTable 随玩家释放自动清理）；卡实例在队列中持有引用，
/// 若排队期间卡已进入牌堆（被打出/洗入）则跳过。
/// </summary>
public static class JainaPendingRewardQueue
{
    private sealed class Entry
    {
        public required CardModel Card;
    }

    private static readonly ConditionalWeakTable<Player, Queue<Entry>> Queues = new();

    /// <summary>
    /// 发放任务奖励：手牌有空位立即入手；手牌满则入队等待空位。
    /// </summary>
    public static async Task GrantOrQueue(PlayerChoiceContext choiceContext, Player player, CardModel card)
    {
        if (card == null || player == null || player.Creature?.CombatState == null)
        {
            return;
        }
        if (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
        {
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
            await TryFlush(player);
            return;
        }
        var queue = Queues.GetValue(player, _ => new Queue<Entry>());
        queue.Enqueue(new Entry { Card = card });
    }

    /// <summary>
    /// 手牌有空位时发放队列中的积压奖励（抽取/入手/打出后由 patch 调用）。
    /// </summary>
    public static async Task TryFlush(Player player)
    {
        if (player == null || player.Creature?.CombatState == null ||
            !Queues.TryGetValue(player, out var queue) || queue.Count == 0)
        {
            return;
        }
        while (queue.Count > 0 && !jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
        {
            var entry = queue.Dequeue();
            if (entry.Card == null || entry.Card.Pile != null)
            {
                continue; // 卡已进入牌堆（排队期间被打出/洗入）：跳过
            }
            var result = await CardPileCmd.AddGeneratedCardToCombat(entry.Card, PileType.Hand, player);
            // 防御：满手改道（卡进弃牌堆而非手牌）说明已无手牌空间——停止发放，
            // 防止"满手判定不一致"导致同一张奖励被反复改道（与唤醒同款防御）
            if (result.cardAdded?.Pile?.Type != PileType.Hand)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 任意卡牌进入牌堆后检查：手牌出现空位（打出卡/抽牌/入手）时发放积压的任务奖励。
    /// 只 patch 单卡 + PileType 的主重载（其他重载委托它或单独处理）。
    /// </summary>
    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add),
        new Type[] { typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool) })]
    public static class AddPostfix
    {
        private static async void Postfix(CardModel card)
        {
            try
            {
                var owner = card?.Owner;
                if (owner != null)
                {
                    await TryFlush(owner);
                }
            }
            catch
            {
                // 奖励发放失败不影响原流程
            }
        }
    }

    /// <summary>
    /// 生成卡加入战斗（入手）后同样检查（AddGeneratedCardToCombat 不走主 Add）。
    /// </summary>
    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AddGeneratedCardToCombat))]
    public static class AddGeneratedPostfix
    {
        private static async void Postfix(CardModel card, Player? creator)
        {
            try
            {
                if (card?.Owner != null)
                {
                    await TryFlush(card.Owner);
                }
                else if (creator != null)
                {
                    await TryFlush(creator);
                }
            }
            catch
            {
                // 奖励发放失败不影响原流程
            }
        }
    }
}
