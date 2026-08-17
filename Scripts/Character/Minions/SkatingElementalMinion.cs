using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 滑冰元素 (Sleet Skater) - 吉安娜专属随从。
/// 属性：攻击 3，生命 4。
/// 战吼：给于1名敌方1层冻结，获得等同于其减少的总体伤害的格挡。
/// （1层冻结使敌方攻击伤害减少 25%；减少的总体伤害 = 该敌方意图攻击伤害 × 25%）
/// </summary>
[RegisterMonster]
public sealed class SkatingElementalMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    /// <summary>
    /// 战斗视觉：滑冰元素卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/sleet_skater.png";

    /// <summary>
    /// 战吼：给于1名敌方1层冻结；获得格挡 = 该敌方因冻结减少的总体伤害
    /// （1层冻结使敌方攻击伤害减少 25%——按该敌方当前意图攻击伤害 × 25% 计算）。
    /// 随从战吼无法选目标：随机选择一名敌方。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rng = owner.RunState.Rng.CombatTargets;

        // 给于1名敌方1层冻结（随机一名敌方）
        var enemies = combatState.Creatures
            .Where(c => c != null && c.IsAlive && c.Side != Creature.Side)
            .ToList();
        if (enemies.Count == 0)
        {
            return;
        }
        var enemy = rng.NextItem(enemies);
        if (enemy == null)
        {
            return;
        }
        await PowerCmd.Apply<FreezePower>(choiceContext, [enemy], 1m, Creature, null);

        // 获得等同于其减少的总体伤害的格挡：
        // 1层冻结使该敌方攻击伤害减少 25%——按敌方当前意图攻击伤害计算减少量
        int totalAttack = 0;
        if (enemy.Monster?.NextMove != null)
        {
            foreach (var intent in enemy.Monster.NextMove.Intents)
            {
                if (intent is AttackIntent atk)
                {
                    totalAttack += atk.GetTotalDamage(new[] { enemy }, enemy);
                }
            }
        }
        int block = (int)(totalAttack * 0.25m);
        if (block > 0)
        {
            await CreatureCmd.GainBlock(owner.Creature, block, ValueProp.Move, null);
        }
    }
}
