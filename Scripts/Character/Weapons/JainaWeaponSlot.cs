using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Weapons;

/// <summary>
/// 武器槽管理（炉石传说式武器位，由 <see cref="JainaWeaponPower"/> 承载）。
/// - 顶替：打出第二张武器能力卡时，旧武器能力（含攻击力/剩余耐久）被新武器完全替换。
/// - 攻击次数：角色每回合最多攻击一次，切换武器不重置（见 EnsureAttackAction 的幂等逻辑）。
/// - 耐久：角色每攻击一次耐久 -1，归零时武器能力消失（ConsumeDurability）。
/// </summary>
public static class JainaWeaponSlot
{
    /// <summary>
    /// 装备武器：顶替旧的武器能力（若有），挂载新武器能力。
    /// 在武器能力卡的 OnPlay 中调用。
    /// </summary>
    /// <param name="attack">武器攻击力</param>
    /// <param name="durability">武器初始耐久度</param>
    public static async Task Equip(PlayerChoiceContext choiceContext, Player player, int attack, int durability,
        CardModel weaponCard)
    {
        if (player == null || weaponCard == null || durability <= 0)
        {
            return;
        }

        // 顶替：移除旧武器能力（含攻击力/剩余耐久全部被新武器替换）
        var old = player.Creature.Powers.OfType<JainaWeaponPower>().FirstOrDefault();
        if (old != null)
        {
            await PowerCmd.Remove(old);
        }

        // 挂载新武器能力（Amount = 耐久度，Attack = 攻击力）
        var power = (JainaWeaponPower)ModelDb.Power<JainaWeaponPower>().ToMutable();
        power.SetWeaponStats(attack);
        await PowerCmd.Apply(choiceContext, power, player.Creature, durability, player.Creature, weaponCard);

        // 赋予本回合攻击行动点（幂等：已有则不重复赋予，保证每回合最多一次）
        await EnsureAttackAction(choiceContext, player);
    }

    /// <summary>
    /// 确保角色拥有武器攻击行动点（每回合 1 次）。
    /// 幂等：已有行动点（无论剩余几次）则不动——切换武器不会重置攻击次数，
    /// 因此"一回合只能攻击一次，无论切换多少张武器"。
    /// </summary>
    public static async Task EnsureAttackAction(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == null)
        {
            return;
        }
        var creature = player.Creature;
        if (creature.Powers.OfType<JainaWeaponAttackAction>().Any())
        {
            return;
        }
        // 没有武器则不赋予行动点
        if (!creature.Powers.OfType<JainaWeaponPower>().Any())
        {
            return;
        }
        await PowerCmd.Apply<JainaWeaponAttackAction>(choiceContext, creature, 1m, creature, null);
    }

    /// <summary>
    /// 角色用武器攻击一次后：武器耐久度 -1；归零时武器能力消失（连同攻击行动点）。
    /// 由 <see cref="JainaWeaponAttackAction.OnAct"/> 调用。
    /// </summary>
    public static async Task ConsumeDurability(PlayerChoiceContext choiceContext, Creature owner,
        JainaWeaponPower weapon)
    {
        if (weapon == null)
        {
            return;
        }
        if (weapon.Amount <= 1)
        {
            // 耐久归零：武器能力消失，同时收回未用的攻击行动点
            await PowerCmd.Remove(weapon);
            var action = owner.Powers.OfType<JainaWeaponAttackAction>().FirstOrDefault();
            if (action != null)
            {
                await PowerCmd.Remove(action);
            }
            return;
        }
        await PowerCmd.Decrement(weapon);
    }
}
