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
        // 记录本局死亡过的骷髅（统计/预留，按主人区分）
        var rec = jaina.Scripts.Character.JainaCastTracker.For(state);
        if (Creature.PetOwner != null)
        {
            rec.SkeletonDeathsByPlayer.TryGetValue(Creature.PetOwner.NetId, out var deaths);
            rec.SkeletonDeathsByPlayer[Creature.PetOwner.NetId] = deaths + 1;
        }
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
        // 亡语伤害：来源改为骷髅的主人（玩家）——引擎禁止"已死 dealer"造成伤害，
        // 与死神之躯的"放不下骷髅爆炸"一致：
        // 玩家作 dealer + ValueProp.Unpowered 固定 2 点（不吃力量/专注，炉石亡语伤害语义）。
        var dealer = Creature.PetOwner?.Creature;
        if (dealer == null)
        {
            return;
        }
        var results = (await CreatureCmd.Damage(choiceContext, [target], DeathrattleDamage, ValueProp.Unpowered, dealer)).ToList();
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[JainaDeathrattle] VolatileSkeleton damage: target={target.Monster?.GetType().Name} " +
            $"unblocked={results.Sum(r => r.UnblockedDamage)} blocked={results.Sum(r => r.BlockedDamage)}");
    }
}