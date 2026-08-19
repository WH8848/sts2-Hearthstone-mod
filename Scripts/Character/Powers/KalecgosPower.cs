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
/// 光环效果：挂在随从生物自身，随从死亡时本 Power 随生物移除、效果消失。
/// 通过费用修改钩子实时生效：本回合尚未使用攻击/技能牌时，
/// 主人所有攻击/技能牌的展示与结算费用为 0；第一张攻击/技能牌打出后恢复原价。
/// </summary>
[RegisterPower]
public sealed class KalecgosPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 本回合是否已使用攻击/技能牌
    /// </summary>
    private bool _usedThisTurn;

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (_usedThisTurn)
        {
            return false;
        }
        var owner = Owner?.PetOwner;
        if (owner == null || card.Owner != owner)
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
        if (!_usedThisTurn && cardPlay.Card.Owner == Owner?.PetOwner)
        {
            // 自动打出（AutoPlay：匣中古神/惊奇卡牌/戏法图腾/大法师的符文/罗曼斯/
            // 灰贤鹦鹉/诈骗犯重放等随机释放）不算"你使用的第一张法术"——它们免费打出
            // 不需要减费，也不应占用本回合的减费名额（否则符文随机打出的法术会
            // 导致玩家随后手打的第一张法术不再减费）
            if (cardPlay.IsAutoPlay)
            {
                return Task.CompletedTask;
            }
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
