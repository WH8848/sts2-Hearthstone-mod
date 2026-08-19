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
    /// 注意：攻击行动点是角色每回合固有的 1 点（与武器无关，见 Entry 战斗开始挂载），
    /// 武器只赋予攻击力，这里不涉及行动点。
    /// </summary>
    /// <param name="attack">武器攻击力</param>
    /// <param name="durability">武器初始耐久度</param>
    /// <param name="weaponCard">武器来源卡（仅记录用途；战吼等无卡实例的场合可为 null，
    /// 不影响装备——卡德加战吼曾因传 null 导致武器静默不装备）</param>
    public static async Task Equip(PlayerChoiceContext choiceContext, Player player, int attack, int durability,
        CardModel? weaponCard)
    {
        if (player == null || durability <= 0)
        {
            return;
        }

        // 顶替：移除旧武器能力（含攻击力/剩余耐久全部被新武器替换）
        // 炉石规则：武器被替换时触发旧武器亡语（OnDestroyed 回调）
        var old = player.Creature.Powers.OfType<JainaWeaponPower>().FirstOrDefault();
        if (old != null)
        {
            if (old.OnDestroyed != null)
            {
                await old.OnDestroyed(choiceContext);
            }
            await PowerCmd.Remove(old);
        }

        // 挂载新武器能力（Amount = 耐久度，Attack = 攻击力）
        var power = (JainaWeaponPower)ModelDb.Power<JainaWeaponPower>().ToMutable();
        power.SetWeaponStats(attack);
        await PowerCmd.Apply(choiceContext, power, player.Creature, durability, player.Creature, weaponCard);

        // 装备武器后刷新玩家攻击意图（可攻击时显示等同于攻击力的攻击意图）
        PlayerAttackIntentPatch.Refresh(player.Creature);
    }

    /// <summary>
    /// 确保角色拥有武器攻击行动点（角色固有的 1 点攻击行动点，战斗开始时挂载）。
    /// 幂等：已有行动点则不动。行动点与武器无关——未装备武器时攻击力为 0，不可行动
    /// （由 JainaWeaponAttackAction.CanAct 检查）。
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
        await PowerCmd.Apply<JainaWeaponAttackAction>(choiceContext, creature, 1m, creature, null);
    }

    /// <summary>
    /// 角色用武器攻击一次后：武器耐久度 -1；归零时武器能力消失。
    /// 攻击行动点保留（角色固有的行动点，武器只赋予攻击力；攻击力归 0 后 CanAct 会阻止行动）。
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
            // 耐久归零：武器能力消失（行动点保留，但攻击力为 0 不可行动）
            // 触发武器亡语（OnDestroyed 回调）
            if (weapon.OnDestroyed != null)
            {
                await weapon.OnDestroyed(choiceContext);
            }
            await PowerCmd.Remove(weapon);
            // 武器消失：刷新玩家攻击意图（攻击力归 0，意图消失）
            PlayerAttackIntentPatch.Refresh(owner);
            return;
        }
        await PowerCmd.Decrement(weapon);
        // 耐久变化后刷新玩家攻击意图
        PlayerAttackIntentPatch.Refresh(owner);
    }
}
