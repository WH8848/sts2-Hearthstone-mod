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
/// 手动模式：玩家可点击攻击（有行动点），回合末不自动攻击。
/// 亡语：随机对一个敌人造成 2 点伤害。
/// </summary>
[RegisterMonster]
public sealed class VolatileSkeleton : JainaMinionBase
{
    /// <summary>
    /// 手动模式：玩家可点击攻击（有行动点），回合末不自动攻击
    /// </summary>
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    /// <summary>
    /// 战斗视觉：不稳定的骷髅卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/volatile_skeleton.png";

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
    /// 注意：亡语在随从死亡后触发（AfterDeath），此时 Creature.IsAlive 为 false，
    /// 不要加存活守卫。
    /// </summary>
    public override async Task OnDeathrattle(PlayerChoiceContext choiceContext)
    {
        // 容错：死亡流程中 Creature.CombatState 可能已被清空，回退到主人的战斗状态
        var state = Creature.CombatState ?? Creature.PetOwner?.Creature.CombatState;
        if (state == null)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn(
                $"[JainaDeathrattle] VolatileSkeleton: no combat state at death (creatureStateNull={Creature.CombatState == null})");
            return;
        }
        // 记录本局死亡过的骷髅（天定之灾克尔苏加德战吼"复活你的不稳定的骷髅"用）
        jaina.Scripts.Character.JainaCastTracker.For(state).SkeletonDeaths++;
        var opponents = state
            .GetOpponentsOf(Creature)
            .Where(e => e != null && e.IsAlive && e.IsHittable)
            .ToList();
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[JainaDeathrattle] VolatileSkeleton opponents={opponents.Count} stateNull={Creature.CombatState == null}");
        if (opponents.Count == 0)
        {
            return;
        }
        var target = state.RunState.Rng.CombatTargets.NextItem(opponents);
        if (target == null)
        {
            return;
        }
        // 炉石亡语语义：伤害来源是死掉的随从本身，效果不因随从死亡而失效。
        // 引擎对"已死 dealer"默认返回空伤害，由 JainaDeathrattleDamagePatch 在亡语结算期间放行。
        JainaDeathrattleHelper.IsResolvingDeathrattle = true;
        try
        {
            await CreatureCmd.Damage(choiceContext, [target], DeathrattleDamage, ValueProp.Unpowered, Creature);
        }
        finally
        {
            JainaDeathrattleHelper.IsResolvingDeathrattle = false;
        }
    }
}