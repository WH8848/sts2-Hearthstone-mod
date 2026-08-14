using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 火焰结界：吉安娜或其随从受到攻击时，对敌人造成 7 次 Amount 点伤害（每次随机分配到一个敌人），随后消失。
/// 挂一次性（参照寒冰护盾 IceBarrierPower 模式）：受击触发后移除；若整回合未被攻击，下个玩家回合开始兜底移除。
/// </summary>
[RegisterPower]
public sealed class FlameWardPower : PowerModel
{
    /// <summary>随机攻击次数</summary>
    private const int Hits = 7;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // target 是自己，或是自己的随从（随从受击伤害转入主人护甲）
        bool isOwnerOrPet = target == Owner || target.PetOwner?.Creature == Owner;
        if (!isOwnerOrPet || amount <= 0 || Amount <= 0)
        {
            return;
        }

        var combatState = Owner.CombatState;
        for (int i = 0; i < Hits; i++)
        {
            var enemies = combatState.GetOpponentsOf(Owner)
                .Where(e => e.IsAlive && e.IsHittable)
                .ToList();
            if (enemies.Count == 0)
            {
                break;
            }
            var targetEnemy = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
            await CreatureCmd.Damage(choiceContext, [targetEnemy], Amount, ValueProp.Unpowered, Owner);
        }

        // 一次性结界：触发后消失
        await PowerCmd.Remove(this);
    }
}
