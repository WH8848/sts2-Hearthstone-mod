using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 观星者露娜：在你使用最右边的一张手牌后，抽一张牌。
/// </summary>
[RegisterMonster]
public sealed class LunaMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;
    public override int MinInitialHp => 4;
    public override int MaxInitialHp => 4;
    protected override string MinionVisualsPath => "res://assets/card_art/stargazer_luna.png";

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Creature is { IsAlive: true, PetOwner: { } owner } && cardPlay.Player == owner &&
            JainaHandOrderTracker.ConsumeRightmostPlay(cardPlay, this))
        {
            return CardPileCmd.Draw(choiceContext, 1, owner);
        }

        return Task.CompletedTask;
    }
}
