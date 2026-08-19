using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 火妖 (Flamewaker) - 吉安娜专属随从。
/// 属性：攻击 2，生命 4。在你施放一个法术后，造成 2 次 1 点伤害，随机分配到所有敌人身上。
/// </summary>
[RegisterMonster]
public sealed class FlamewakerMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    protected override string MinionVisualsPath => "res://assets/card_art/flamewaker.png";

    /// <summary>
    /// 施放法术后：造成 2 次 1 点伤害，每次随机分配到一名存活敌人身上
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!Creature.IsAlive || Creature.PetOwner == null || cardPlay.Card.Owner != Creature.PetOwner)
        {
            return;
        }
        var type = cardPlay.Card.Type;
        if (type != CardType.Attack && type != CardType.Skill)
        {
            return;
        }
        // 英雄技能卡（火焰冲击/二级火焰冲击/奥术爆裂/冰冷触摸）不是法术牌，不触发火妖
        // （英雄技能卡类型为 Attack，仅按类型判断会误触发）
        if (jaina.Scripts.Character.Powers.HeroPowerHandHelper.IsHeroPowerCard(cardPlay.Card))
        {
            return;
        }
        var combatState = Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        for (int i = 0; i < 2; i++)
        {
            var enemies = combatState.GetOpponentsOf(Creature)
                .Where(e => e != null && e.IsAlive && e.IsHittable)
                .ToList();
            if (enemies.Count == 0)
            {
                break;
            }
            var target = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
            if (target == null)
            {
                break;
            }
            // 无来源伤害（非攻击）：不吃力量加成，也不触发随从伤害冻结（水元素）
            await CreatureCmd.Damage(choiceContext, [target], 1m, ValueProp.Unpowered, Creature);
        }
    }
}
