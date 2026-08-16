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
/// 手牌顺序由共享的 JainaHandOrderTracker 维护（多只露娜判定一致）：
/// 召唤/回合开始重建，抽牌/生成牌追加末尾，打出时定位判定并移除。
/// 注意：Hook.BeforeCardPlayed 调用时卡已移出手牌（AddDuringManualCardPlay 在前），
/// 因此必须依赖快照而不是实时手牌列表。
/// </summary>
[RegisterMonster]
public sealed class LunaMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    protected override string MinionVisualsPath => "res://assets/card_art/stargazer_luna.png";

    /// <summary>
    /// 记录本回合打出的牌是否为手牌最右边（打出前判定）
    /// </summary>
    private bool _playedRightmost;

    /// <summary>
    /// 召唤时立即初始化共享快照（回合中途召唤时 BeforeSideTurnStart 已过）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        if (Creature.PetOwner != null)
        {
            JainaHandOrderTracker.Rebuild(Creature.PetOwner, Creature.PetOwner.PlayerCombatState?.Hand?.Cards);
        }
    }

    /// <summary>
    /// 玩家回合开始：先调用基类（授予手动模式的点击攻击行动点），再重建共享快照
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        // 基类：手动模式授予本回合攻击行动点（不调用则露娜永远无法点击攻击）
        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
        if (side == Creature.Side && Creature.PetOwner != null)
        {
            JainaHandOrderTracker.Rebuild(Creature.PetOwner, Creature.PetOwner.PlayerCombatState?.Hand?.Cards);
        }
    }

    /// <summary>
    /// 抽牌时把新卡追加到快照末尾（新卡在最右）
    /// </summary>
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (Creature.PetOwner != null && card.Owner == Creature.PetOwner)
        {
            JainaHandOrderTracker.Append(Creature.PetOwner, card);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 生成牌入手时同样追加到快照末尾
    /// </summary>
    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (Creature.PetOwner != null && card.Owner == Creature.PetOwner)
        {
            JainaHandOrderTracker.Append(Creature.PetOwner, card);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 打出前：判定该牌是否为共享快照末尾（最右）。
    /// 只判定不移除——多只露娜必须看到同一快照（移除统一在 AfterCardPlayed）。
    /// </summary>
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        _playedRightmost = false;
        if (!Creature.IsAlive || cardPlay.Card.Owner != Creature.PetOwner || Creature.PetOwner == null)
        {
            return Task.CompletedTask;
        }
        _playedRightmost = JainaHandOrderTracker.IsRightmost(Creature.PetOwner, cardPlay.Card);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 使用最右边的手牌后抽一张牌（光环：仅露娜在场时生效）。
    /// 这里才从共享快照移除打出卡（所有露娜的判定已完成）。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Creature.PetOwner != null && cardPlay.Card.Owner == Creature.PetOwner)
        {
            JainaHandOrderTracker.Remove(Creature.PetOwner, cardPlay.Card);
        }
        if (!_playedRightmost || !Creature.IsAlive)
        {
            return;
        }
        _playedRightmost = false;
        await CardPileCmd.Draw(choiceContext, 1, Creature.PetOwner!);
    }
}
