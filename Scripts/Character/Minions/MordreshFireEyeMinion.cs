using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 火眼莫德雷斯 (Mordresh Fire Eye) - 吉安娜专属随从。
/// 属性：攻击 8，生命 8。
/// 战吼：在本局对战中，如果你用你的英雄技能累计造成了10点伤害，
/// 则对所有敌人造成4次10点伤害。仅手牌打出时触发。
/// </summary>
[RegisterMonster]
public sealed class MordreshFireEyeMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 8;

    public override int MaxInitialHp => 8;

    /// <summary>
    /// 战斗视觉：火眼莫德雷斯卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/mordresh_fire_eye.png";

    /// <summary>
    /// 战吼：英雄技能本局累计造成 10 点伤害后，对所有敌人造成 4 次 10 点伤害。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        var combatState = Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        rec.HeroPowerDamageDealtByPlayer.TryGetValue(owner.NetId, out var heroPowerDamage);
        if (heroPowerDamage < 10)
        {
            return;
        }

        // 对所有敌人造成 4 次 10 点伤害
        var enemies = combatState.GetOpponentsOf(Creature)
            .Where(e => e != null && e.IsAlive && e.IsHittable)
            .ToList();
        if (enemies.Count == 0)
        {
            return;
        }
        for (int i = 0; i < 4; i++)
        {
            // Move 标记：吃力量加成（与原版多次攻击牌一致）
            await CreatureCmd.Damage(choiceContext, enemies, 10m, ValueProp.Move, Creature, null, null);
        }
    }
}
