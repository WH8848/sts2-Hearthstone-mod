using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 巫师学徒 (Sorcerer's Apprentice) - 吉安娜专属随从。
/// 属性：攻击 3，生命 2。
/// 你每打出四张攻击牌或技能牌，下一张攻击牌或技能牌消耗减少1点。
/// </summary>
[RegisterMonster]
public sealed class SorcererApprenticeMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 2;

    public override int MaxInitialHp => 2;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/sorcerer_apprentice.tscn";

    /// <summary>
    /// 打出攻击/技能牌计数（达到 4 张后给手牌中一张攻击/技能牌减 1 费）
    /// </summary>
    private int _castCount;

    /// <summary>
    /// 打出攻击/技能牌时计数；凑满 4 张把手牌中一张攻击/技能牌费用 -1
    /// </summary>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Creature.PetOwner || Creature.PetOwner == null)
        {
            return Task.CompletedTask;
        }
        var type = cardPlay.Card.Type;
        if (type != CardType.Attack && type != CardType.Skill)
        {
            return Task.CompletedTask;
        }
        _castCount++;
        if (_castCount >= 4)
        {
            TryApplyDiscount();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 给手牌中第一张费用大于 0 的攻击/技能牌减 1 费（直到打出）。
    /// 手牌中暂时没有攻击/技能牌时保持挂起（计数持续 ≥4，每次打出后重试）。
    /// </summary>
    private void TryApplyDiscount()
    {
        var hand = Creature.PetOwner?.PlayerCombatState?.Hand?.Cards;
        if (hand == null)
        {
            return;
        }
        var target = hand.FirstOrDefault(c =>
            (c.Type == CardType.Attack || c.Type == CardType.Skill) && c.EnergyCost.GetResolved() > 0);
        if (target != null)
        {
            target.EnergyCost.AddUntilPlayed(-1);
            _castCount = 0;
        }
    }
}
