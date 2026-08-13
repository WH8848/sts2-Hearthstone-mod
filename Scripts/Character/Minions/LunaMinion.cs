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
using MinionLib.Minion;
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
    /// 召唤时立即初始化手牌快照（回合中途召唤时 BeforeSideTurnStart 已过，
    /// 快照为空会导致召唤当回合所有判定 index=-1）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        _handOrder.Clear();
        var hand = Creature.PetOwner?.PlayerCombatState?.Hand?.Cards;
        if (hand != null)
        {
            _handOrder.AddRange(hand);
        }
    }

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
            MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] Luna TurnStart: snapshotCount={_handOrder.Count}");
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
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] Luna BeforePlay: card={cardPlay.Card.Id.Entry} alive={Creature.IsAlive} snapCount={_handOrder.Count}");
        if (!Creature.IsAlive || cardPlay.Card.Owner != Creature.PetOwner || Creature.PetOwner == null)
        {
            return Task.CompletedTask;
        }
        int index = _handOrder.IndexOf(cardPlay.Card);
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] Luna BeforePlay: index={index} isRightmost={(index >= 0 && index == _handOrder.Count - 1)}");
        if (index >= 0)
        {
            _playedRightmost = index == _handOrder.Count - 1;
            _handOrder.RemoveAt(index);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 使用最右边的手牌后抽一张牌（光环：仅露娜在场时生效）。
    /// 召唤当回合（打出露娜时钩子尚未挂载）快照可能残留本卡，
    /// 这里兜底清理，保证多只露娜各自快照始终同步。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 兜底：把打出卡从快照移除（当回合残留的露娜卡/任何未移除的卡）
        if (cardPlay.Card.Owner == Creature.PetOwner)
        {
            _handOrder.Remove(cardPlay.Card);
        }
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] Luna AfterPlay: card={cardPlay.Card.Id.Entry} flag={_playedRightmost} alive={Creature.IsAlive}");
        if (!_playedRightmost || !Creature.IsAlive)
        {
            return;
        }
        _playedRightmost = false;
        await CardPileCmd.Draw(choiceContext, 1, Creature.PetOwner!);
    }
}
