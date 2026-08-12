using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 不稳定的骷髅 (Volatile Skeleton) - 吉安娜专属随从。
/// 属性：攻击 2，生命 2。
/// 亡语：随机对一个敌人造成 2 点伤害。
/// </summary>
[RegisterMonster]
public sealed class VolatileSkeleton : JainaMinionBase
{
    /// <summary>
    /// 自动模式：回合结束自动攻击随机敌人
    /// </summary>
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Auto;

    /// <summary>
    /// 战斗视觉：不稳定的骷髅卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/minion_visuals/volatile_skeleton.tscn";

    /// <summary>
    /// 亡语伤害值
    /// </summary>
    public int DeathrattleDamage => 2;

    public override int MinInitialHp => 2;

    public override int MaxInitialHp => 2;

    /// <summary>
    /// 拥有亡语词条
    /// </summary>
    public override bool HasDeathrattle => true;

    /// <summary>
    /// 亡语：随机对一个敌人造成 2 点伤害。
    /// </summary>
    public override async Task OnDeathrattle(PlayerChoiceContext choiceContext)
    {
        if (!Creature.IsAlive || Creature.CombatState == null)
        {
            return;
        }
        var opponents = Creature.CombatState
            .GetOpponentsOf(Creature)
            .Where(e => e != null && e.IsAlive && e.IsHittable)
            .ToList();
        if (opponents.Count == 0)
        {
            return;
        }
        var target = CombatState.RunState.Rng.CombatTargets.NextItem(opponents);
        if (target == null)
        {
            return;
        }
        await CreatureCmd.Damage(choiceContext, [target], DeathrattleDamage, ValueProp.Unpowered, Creature);
    }
}