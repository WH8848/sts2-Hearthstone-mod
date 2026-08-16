using System;
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
/// 天定之灾克尔苏加德 (Kel'Thuzad, the Inevitable) - 吉安娜专属随从。
/// 属性：攻击 6，生命 8。
/// 战吼：复活你的不稳定的骷髅（本局对战中死亡过的骷髅，按空位数量召唤 2/2）。
/// 战场上放不下的骷髅会立即爆炸：每个对随机敌人造成 2 点伤害（亡语伤害）。
/// </summary>
[RegisterMonster]
public sealed class KelThuzadMinion : JainaMinionBase
{
    /// <summary>
    /// 战斗视觉：天定之灾克尔苏加德卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/kelthuzad_inevitable.png";

    public override int MinInitialHp => 8;

    public override int MaxInitialHp => 8;

    /// <summary>
    /// 战吼：复活本局死亡过的不稳定的骷髅；放不下的立即爆炸（对随机敌人造成 2 点伤害）。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        var state = owner.Creature.CombatState;
        if (state == null)
        {
            return;
        }
        int died = jaina.Scripts.Character.JainaCastTracker.For(state).SkeletonDeaths;
        if (died <= 0)
        {
            return;
        }

        // 空位 = 上限 7 - 当前随从数；放不下的骷髅立即爆炸
        int emptySlots = Math.Max(0, JainaMinionPool.MaxMinions - JainaMinionPool.GetCurrentMinionCount(owner));
        int toSummon = Math.Min(died, emptySlots);
        int toExplode = died - toSummon;

        for (int i = 0; i < toSummon; i++)
        {
            await JainaMinionPool.SummonMinionByType(
                choiceContext, owner, typeof(VolatileSkeleton), maxHp: 2, attack: 2);
        }

        // 爆炸：每个放不下的骷髅对随机敌人造成 2 点伤害（不稳定的骷髅的亡语伤害）
        for (int i = 0; i < toExplode; i++)
        {
            var enemies = state.GetOpponentsOf(owner.Creature)
                .Where(e => e != null && e.IsAlive && e.IsHittable)
                .ToList();
            if (enemies.Count == 0)
            {
                break;
            }
            var target = state.RunState.Rng.CombatTargets.NextItem(enemies);
            if (target == null)
            {
                continue;
            }
            await CreatureCmd.Damage(choiceContext, [target], 2, ValueProp.Unpowered, owner.Creature);
        }
    }
}
