using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 巫师学徒光环：你每打出四张攻击牌或技能牌，下一张攻击牌或技能牌消耗减少1点。
/// 挂在随从生物自身——随从死亡时本 Power 随生物移除（计数与折扣随之失效）。
/// </summary>
[RegisterPower]
public sealed class SorcererApprenticePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 打出攻击/技能牌计数（达到 4 张后给手牌中一张攻击/技能牌减 1 费）
    /// </summary>
    private int _castCount;

    /// <summary>
    /// 打出攻击/技能牌时计数；凑满 4 张把手牌中一张攻击/技能牌费用 -1
    /// </summary>
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner?.PetOwner;
        if (owner == null || cardPlay.Card.Owner != owner)
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
        var hand = Owner?.PetOwner?.PlayerCombatState?.Hand?.Cards;
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
