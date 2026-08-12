using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 决斗大师莫扎奇 (Mozaki Master of Flame) - 吉安娜专属随从。
/// 属性：攻击 3，生命 8。在你施放一张攻击牌或技能牌后，获得力量+1。
/// </summary>
[RegisterMonster]
public sealed class MozakiMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 8;

    public override int MaxInitialHp => 8;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/mozaki.tscn";

    /// <summary>
    /// 施放攻击/技能牌后，玩家获得力量+1
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
        if (Creature.PetOwner != null)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, [Creature.PetOwner.Creature], 1m, Creature, null);
        }
    }
}
