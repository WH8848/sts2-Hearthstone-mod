using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 奥数工匠光环：每当你打出一张攻击牌或技能牌，便获得等同于其费用的护甲值。
/// 挂在随从生物自身——随从死亡时本 Power 随生物移除，被动自动失效。
/// </summary>
[RegisterPower]
public sealed class ArcaneArtificerPower : PowerModel
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
        var cost = cardPlay.Card.EnergyCost.Canonical;
        if (cost > 0)
        {
            await CreatureCmd.GainBlock(owner.Creature, cost, ValueProp.Move, null);
        }
    }
}
