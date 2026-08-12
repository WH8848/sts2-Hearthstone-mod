using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 观星者露娜 (Stargazer Luna) - 吉安娜专属随从。
/// 属性：攻击 2，生命 4。在你使用一张手牌后，抽一张牌。
/// </summary>
[RegisterMonster]
public sealed class LunaMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/stargazer_luna.tscn";

    /// <summary>
    /// 使用任意手牌后抽一张牌
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Creature.PetOwner || Creature.PetOwner == null)
        {
            return;
        }
        await CardPileCmd.Draw(choiceContext, 1, Creature.PetOwner);
    }
}
