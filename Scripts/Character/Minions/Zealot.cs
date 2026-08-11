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
/// 狂热者 (Zealot) - 吉安娜专属随从。
/// 属性：攻击 3，生命 4。
/// 效果：被生成时立刻攻击一次（随机攻击一个敌人）。回合结束攻击。
/// </summary>
[RegisterMonster]
public sealed class Zealot : JainaMinionBase
{
    /// <summary>
    /// 自动模式：被生成时立刻攻击 + 回合结束自动攻击
    /// </summary>
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Auto;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    /// <summary>
    /// 被生成时立刻攻击一次
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        if (options.PrimaryStatAmount is decimal attack)
        {
            BaseAttackValue = (int)attack;
        }

        // 立刻攻击一个随机敌人
        var opponents = Creature.CombatState?.GetOpponentsOf(Creature)
            .Where(e => e != null && e.IsAlive && e.IsHittable)
            .ToList();
        if (opponents == null || opponents.Count == 0) return;
        var target = CombatState.RunState.Rng.CombatTargets.NextItem(opponents);
        if (target == null) return;
        await CreatureCmd.Damage(choiceContext, [target], BaseAttackValue, ValueProp.Unpowered, Creature);
    }
}