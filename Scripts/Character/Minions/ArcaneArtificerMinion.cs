using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 奥数工匠 (Arcane Artificer) - 吉安娜专属随从。
/// 属性：攻击 1，生命 3。每当你打出一张攻击牌或技能牌，便获得等同于其费用的护甲值。
/// </summary>
[RegisterMonster]
public sealed class ArcaneArtificerMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 3;

    public override int MaxInitialHp => 3;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/arcane_artificer.tscn";

    /// <summary>
    /// 打出攻击/技能牌时，获得等同于其费用的护甲值
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
        var cost = cardPlay.Card.EnergyCost.Canonical;
        if (cost > 0)
        {
            await CreatureCmd.GainBlock(Creature, cost, ValueProp.Move, null);
        }
    }
}
