using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 火眼莫德雷斯 (Mordresh Fire Eye) - 吉安娜专属随从。
/// 属性：攻击 8，生命 8（亡灵）。
/// 战吼：在本局对战中，如果你用你的英雄技能累计造成了10点伤害，
/// 则对随机敌人造成8次10点伤害（每击独立随机目标，可重叠；目标死亡后剔除）。
/// </summary>
[RegisterMonster]
public sealed class MordreshFireEyeMinion : JainaMinionBase
{
    public override int MinInitialHp => 8;

    public override int MaxInitialHp => 8;

    /// <summary>
    /// 战斗视觉：火眼莫德雷斯卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/mordresh_fire_eye.png";

    /// <summary>
    /// 战吼：英雄技能本局累计造成 10 点伤害后，对随机敌人造成 8 次 10 点伤害。
    /// 随从战吼伤害走 CreatureCmd.Damage（无强化、不触发"被攻击命中"效果）——
    /// 与不稳定的骷髅"放不下的骷髅立即爆炸"同口径；每次独立随机目标、目标死亡后剔除。
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

        // 条件：英雄技能本局累计造成 10 点伤害（火焰冲击记录）
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        rec.HeroPowerDamageDealtByPlayer.TryGetValue(owner.NetId, out var heroPowerDamage);
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[Jaina] Mordresh Fire Eye battlecry: heroPowerDamage={heroPowerDamage} need>=10 -> {(heroPowerDamage >= 10 ? "proceed" : "SKIP no damage")}");
        if (heroPowerDamage < 10)
        {
            return;
        }

        // 对随机敌人造成 8 次 10 点伤害（每次独立随机目标，可重叠；目标死亡后剔除）
        var enemies = combatState.GetOpponentsOf(owner.Creature)
            .Where(e => e != null && e.IsAlive && e.IsHittable)
            .ToList();
        for (int i = 0; i < 8; i++)
        {
            enemies = enemies.Where(e => e.IsAlive).ToList();
            if (enemies.Count == 0)
            {
                break;
            }
            var target = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
            if (target == null)
            {
                break;
            }
            await CreatureCmd.Damage(choiceContext, [target], 10, ValueProp.Unpowered, Creature);
        }
    }
}
