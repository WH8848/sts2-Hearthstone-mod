using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 随从军势 - 吉安娜的随从守卫能力。
/// 当吉安娜的护甲无法阻挡伤害时，按随从召唤顺序扣除随从生命值来抵挡。
/// 所有随从生命值都不足以抵挡时，剩余伤害才会扣到吉安娜。
/// </summary>
[RegisterPower]
public sealed class MinionSquadPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_minion_squad_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 拦截未格挡伤害 - 按随从召唤顺序扣除随从 HP
    /// </summary>
    public override decimal ModifyHpLostBeforeOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 只拦截对吉安娜本体的伤害
        if (target != base.Owner)
        {
            return amount;
        }
        if (amount <= 0m)
        {
            return amount;
        }

        // 获取吉安娜的所有存活随从（生物随从）
        var player = base.Owner.Player;
        if (player?.PlayerCombatState?.Pets == null)
        {
            return amount;
        }

        // 按召唤顺序（Pets 列表顺序即召唤顺序）遍历随从
        var minions = player.PlayerCombatState.Pets
            .Where(p => p != null && p.IsAlive && p.Monster is JainaMinionBase)
            .ToList();

        foreach (var minion in minions)
        {
            decimal absorbed = decimal.Min(amount, minion.CurrentHp);
            if (absorbed > 0m)
            {
                // 扣随从生命（走 LoseHpInternal 避免再次触发挡伤递归）
                minion.LoseHpInternal(absorbed, ValueProp.Unpowered);
                Flash();
                amount -= absorbed;
            }

            // 随从被挡伤致死：LoseHpInternal 不会触发死亡流程，
            // 必须手动走 Kill（触发死亡动画、移除战场节点、亡语），
            // 否则尸体卡图留在场上挡住其他随从。
            if (minion.IsDead)
            {
                _ = CreatureCmd.Kill(minion, force: false);
            }

            if (amount <= 0m)
            {
                break;
            }
        }

        return amount;
    }
}
