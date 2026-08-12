using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 大法师安东尼达斯 (Archmage Antonidas) - 吉安娜专属随从。
/// 属性：攻击 5，生命 7。每当你施放一个攻击牌或技能牌，将一张"火球术"攻击牌置入你的手牌。
/// </summary>
[RegisterMonster]
public sealed class AntonidasMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 7;

    public override int MaxInitialHp => 7;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/antonidas.tscn";

    /// <summary>
    /// 施放攻击/技能牌后，将一张火球术置入手牌
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Creature.PetOwner)
        {
            return;
        }
        var type = cardPlay.Card.Type;
        if (type != CardType.Attack && type != CardType.Skill)
        {
            return;
        }
        await CardPileCmd.AddGeneratedCardToCombat((MegaCrit.Sts2.Core.Models.CardModel)MegaCrit.Sts2.Core.Models.ModelDb.GetById<Fireball>(MegaCrit.Sts2.Core.Models.ModelDb.GetId(typeof(Fireball))).MutableClone(), PileType.Hand, Creature.PetOwner);
    }
}
