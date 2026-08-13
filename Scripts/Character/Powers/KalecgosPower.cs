using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 卡雷苟斯（Kalecgos）：你每个回合使用的第一张攻击牌或技能牌的费用为0点。
/// 挂在玩家身上，通过费用修改钩子实时生效：
/// 本回合尚未使用攻击/技能牌时，主人所有攻击/技能牌的展示与结算费用为 0；
/// 第一张攻击/技能牌打出后（AfterCardPlayed），其余牌恢复原费用。
/// 卡雷苟斯死亡后效果消失（SourceMinion 存活检查）。
/// </summary>
[RegisterPower]
public sealed class KalecgosPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 效果来源随从（随从死亡后效果失效）
    /// </summary>
    public Creature? SourceMinion;

    /// <summary>
    /// 本回合是否已使用攻击/技能牌
    /// </summary>
    private bool _usedThisTurn;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (_usedThisTurn || SourceMinion == null || !SourceMinion.IsAlive)
        {
            return false;
        }
        if (card.Owner != Owner.Player)
        {
            return false;
        }
        if (card.Type != CardType.Attack && card.Type != CardType.Skill)
        {
            return false;
        }
        modifiedCost = 0m;
        return true;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!_usedThisTurn && cardPlay.Card.Owner == Owner.Player)
        {
            var type = cardPlay.Card.Type;
            if (type == CardType.Attack || type == CardType.Skill)
            {
                _usedThisTurn = true;
            }
        }
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side)
        {
            _usedThisTurn = false;
        }
        return Task.CompletedTask;
    }
}
