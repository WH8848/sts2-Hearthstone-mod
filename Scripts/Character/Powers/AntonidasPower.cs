using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 大法师安东尼达斯光环：每当你施放一个攻击牌或技能牌，将一张"火球术"攻击牌置入你的手牌。
/// 挂在随从生物自身——随从死亡时本 Power 随生物移除，被动自动失效。
/// </summary>
[RegisterPower]
public sealed class AntonidasPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner?.PetOwner;
        if (owner == null || cardPlay.Card.Owner != owner)
        {
            return;
        }
        var type = cardPlay.Card.Type;
        if (type != CardType.Attack && type != CardType.Skill)
        {
            return;
        }
        // MutableClone 的卡无 Owner，AddGeneratedCardToCombat 内部会 NRE；用 CreateCard 生成带 Owner 的实例
        var combatState = owner.Creature.CombatState;
        var fireball = combatState.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(Fireball))), owner);
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(fireball);
        await CardPileCmd.AddGeneratedCardToCombat(fireball, PileType.Hand, owner);
    }
}
