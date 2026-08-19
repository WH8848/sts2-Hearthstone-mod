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
/// 战吼：选择1名敌人，给予其1层冻结，获得等同于其减少的总体伤害的格挡。
/// （1层冻结使敌方攻击伤害减少 12.5%；减少的总体伤害 = 该敌方意图攻击伤害 × 12.5%）
/// 冻结不受人工制品阻挡（炉石规则：冻结无视人工制品）。
/// </summary>
[RegisterMonster]
public sealed class SkatingElementalMinion : JainaMinionBase
{
    /// <summary>
    /// 战吼选择的目标（由随从卡 OnPlay 在召唤前静态传入；召唤完成后清除）
    /// </summary>
    public static MegaCrit.Sts2.Core.Entities.Creatures.Creature? BattlecryTargetOverride;

    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    /// <summary>
    /// 战斗视觉：滑冰元素卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/sleet_skater.png";

    /// <summary>
    /// 战吼：选择1名敌人，给予其1层冻结（无视人工制品）；
    /// 获得格挡 = 该敌方因冻结减少的总体伤害
    /// （1层冻结使敌方攻击伤害减少 12.5%——按该敌方当前意图攻击伤害 × 12.5% 计算）。
    /// 选择目标失效时回退随机一名存活敌人。
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

        // 目标：战吼选择的目标（失效时回退随机一名存活敌人）
        var enemy = BattlecryTargetOverride;
        if (enemy == null || !enemy.IsAlive || !enemy.IsHittable || enemy.Side == Creature.Side)
        {
            var rng = owner.RunState.Rng.CombatTargets;
            var enemies = combatState.Creatures
                .Where(c => c != null && c.IsAlive && c.Side != Creature.Side)
                .ToList();
            if (enemies.Count == 0)
            {
                return;
            }
            enemy = rng.NextItem(enemies);
        }
        if (enemy == null)
        {
            return;
        }

        // 给于1层冻结（无视人工制品：滑冰元素的冻结不被人工制品阻挡）
        FreezePower.BypassArtifactNextApply = true;
        try
        {
            await PowerCmd.Apply<FreezePower>(choiceContext, [enemy], 1m, Creature, null);
        }
        finally
        {
            FreezePower.BypassArtifactNextApply = false;
        }

        // 获得等同于其减少的总体伤害的格挡：
        // 1层冻结使该敌方攻击伤害减少 12.5%——按敌方当前意图攻击伤害计算减少量。
        // GetTotalDamage(targets, owner) 的 targets = 被攻击的目标（玩家方），
        // 不是敌人自己——传错目标会导致伤害计算错误（力量/易伤等修正按目标判定），
        // 表现为"冻结给了但格挡没正确获得"。
        int totalAttack = 0;
        if (enemy.Monster?.NextMove != null)
        {
            foreach (var intent in enemy.Monster.NextMove.Intents)
            {
                if (intent is AttackIntent atk)
                {
                    totalAttack += atk.GetTotalDamage(new[] { owner.Creature }, enemy);
                }
            }
        }
        int block = (int)(totalAttack * 0.125m);
        if (block > 0)
        {
            await CreatureCmd.GainBlock(owner.Creature, block, ValueProp.Move, null);
        }
    }
}
