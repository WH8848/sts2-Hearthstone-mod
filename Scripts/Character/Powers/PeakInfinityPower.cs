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
/// 巅峰无限的压轴：回合结束时将本牌移回你的手牌。
/// 挂在玩家身上，记录触发压轴的巅峰无限卡实例；
/// 玩家回合结束时若该卡在弃牌堆，移回手牌并移除本 Power。
/// </summary>
[RegisterPower]
public sealed class PeakInfinityPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 触发压轴的巅峰无限卡
    /// </summary>
    public CardModel? TargetCard;

    /// <summary>
    /// 压轴：下回合开始时，将本牌从弃牌堆移回手牌，本 Power 移除。
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side)
        {
            return;
        }
        if (TargetCard is { Pile.Type: PileType.Discard })
        {
            await CardPileCmd.Add(TargetCard, PileType.Hand);
        }
        await PowerCmd.Remove(this);
    }
}
