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
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 观星者露娜 (Stargazer Luna) - 吉安娜专属随从。
/// 属性：攻击 2，生命 4。在你使用最右边的一张手牌后，抽一张牌。
/// 注意：Hook.BeforeCardPlayed 调用时卡已移出手牌（AddDuringManualCardPlay 在前），
/// 无法直接对比手牌列表，因此维护一份手牌顺序快照：
/// 玩家回合开始快照手牌；抽牌/生成牌追加到末尾；打出时在快照中定位并移除。
/// </summary>
[RegisterMonster]
public sealed class LunaMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    protected override string MinionVisualsPath => "res://assets/card_art/stargazer_luna.png";

    /// <summary>
    /// 本回合手牌顺序快照（与手牌 UI 左右顺序一致，末尾 = 最右）
    /// </summary>
    private readonly List<CardModel> _handOrder = [];

    /// <summary>
    /// 记录本回合打出的牌是否为手牌最右边（打出前判定）
    /// </summary>
    private bool _playedRightmost;

    /// <summary>
    /// 玩家回合开始：重建手牌顺序快照
    /// </summary>
    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Creature.Side)
        {
            _handOrder.Clear();
            var hand = Creature.PetOwner?.PlayerCombatState?.Hand?.Cards;
            if (hand != null)
            {
                _handOrder.AddRange(hand);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 抽牌时把新卡追加到快照末尾（新卡在最右）
    /// </summary>
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner == Creature.PetOwner)
        {
            _handOrder.Add(card);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 生成牌入手时同样追加到快照末尾
    /// </summary>
    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card.Owner == Creature.PetOwner)
        {
            _handOrder.Add(card);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 打出前：在快照中定位该牌，判定是否为最右一张，并从快照移除
    /// </summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        _playedRightmost = false;
        if (!Creature.IsAlive || cardPlay.Card.Owner != Creature.PetOwner || Creature.PetOwner == null)
        {
            return Task.CompletedTask;
        }
        int index = _handOrder.IndexOf(cardPlay.Card);
        if (index >= 0)
        {
            _playedRightmost = index == _handOrder.Count - 1;
            _handOrder.RemoveAt(index);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 使用最右边的手牌后抽一张牌（光环：仅露娜在场时生效）
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_playedRightmost || !Creature.IsAlive)
        {
            return;
        }
        _playedRightmost = false;
        await CardPileCmd.Draw(choiceContext, 1, Creature.PetOwner!);
    }
}
