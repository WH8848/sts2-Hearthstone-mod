using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using jaina.Scripts.Character.Cards;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 星术师索兰莉安 (Astromancer Solarian) - 吉安娜专属随从。
/// 属性：攻击 3，生命 2。力量+1（在场期间玩家力量 +1）。
/// 亡语：将"终极索兰莉安"洗入你的牌库（抽牌堆）。
/// </summary>
[RegisterMonster]
public sealed class AstromancerSolarianMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 2;

    public override int MaxInitialHp => 2;

    protected override string MinionVisualsPath => "res://assets/card_art/astromancer_solarian.png";

    /// <summary>
    /// 拥有亡语词条
    /// </summary>
    public override bool HasDeathrattle => true;

    /// <summary>
    /// 力量+1：常驻效果（任何召唤方式都触发，随从死亡时移除）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);
        await PowerCmd.Apply<StrengthPower>(choiceContext, [owner.Creature], 1m, Creature, options.Source);
    }

    /// <summary>
    /// 亡语：移除 +1 力量，将终极索兰莉安洗入牌库（抽牌堆）
    /// </summary>
    public override async Task OnDeathrattle(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        await PowerCmd.Apply<StrengthPower>(choiceContext, [owner.Creature], -1m, Creature, null);

        // 将终极索兰莉安洗入抽牌堆（随机位置）
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var solarianPrime = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, owner, typeof(SolarianPrimeCard), 0);
        if (solarianPrime != null)
        {
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(solarianPrime);
            await CardPileCmd.Add(solarianPrime, PileType.Draw, CardPilePosition.Random);
        }
    }
}
