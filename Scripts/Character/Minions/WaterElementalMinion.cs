using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 水元素 (Water Elemental) - 吉安娜专属随从。
/// 属性：攻击 3，生命 6。冻结：任何受到本随从伤害的角色获得 1 层冻结。
/// </summary>
[RegisterMonster]
public sealed class WaterElementalMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 6;

    public override int MaxInitialHp => 6;

    protected override string MinionVisualsPath => "res://assets/card_art/water_elemental.png";

    /// <summary>
    /// 造成伤害后：给受伤角色 1 层冻结（手动点击攻击与自动攻击都走 CreatureCmd.Damage → AfterDamageGiven）
    /// </summary>
    public override Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Creature)
        {
            return Task.CompletedTask;
        }
        if (!target.IsAlive)
        {
            return Task.CompletedTask;
        }
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return Task.CompletedTask;
        }
        return PowerCmd.Apply<FreezePower>(choiceContext, [target], 1m, owner.Creature, cardSource);
    }
}
