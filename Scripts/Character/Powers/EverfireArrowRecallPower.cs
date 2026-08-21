using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 永时火焰箭（升级版）的回手效果：下回合开始时，将本牌移回你的手牌。
/// 挂在玩家身上，记录打出过本效果的永时火焰箭卡实例；
/// 玩家下回合开始若该卡不在手牌（弃牌堆/抽牌堆等），移回手牌并移除本 Power。
/// </summary>
[RegisterPower]
public sealed class EverfireArrowRecallPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 触发回手的永时火焰箭卡（支持同回合多张）
    /// </summary>
    public readonly List<CardModel> TargetCards = new();

    /// <summary>
    /// 下回合开始时，将本牌移回手牌，本 Power 移除。
    /// 无论本牌在哪个牌堆（弃牌堆/消耗堆/抽牌堆等）都移回手牌
    /// （只要还在对局中且不在手牌；已完全移出对局的卡无法回手）。
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side)
        {
            return;
        }
        foreach (var card in TargetCards)
        {
            if (card.Pile != null && card.Pile.Type != PileType.Hand)
            {
                await CardPileCmd.Add(card, PileType.Hand);
            }
        }
        await PowerCmd.Remove(this);
    }
}
