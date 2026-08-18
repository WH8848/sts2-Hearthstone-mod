using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 鼠标悬停玩家角色（自己或队友）时，显示该玩家的<b>随从血条</b>（生命值）与
/// <b>攻击意图</b>；移出后隐藏。参考亡灵契约师奥斯提：悬停队友才能查看其随从生命值。
/// 随从平时不显示血条（JainaMinionBase.IsHealthBarVisible = false，保持场面简洁）；
/// 攻击意图同样只在悬停主人时显示（CanShowAttackIntent 检查
/// <see cref="HoveredPlayerNetId"/>）。
/// 只控制 Jaina 随从（奥斯提等原版宠物血条常驻，不受影响）。
/// 纯本地 UI：每端独立控制，联机安全。
/// </summary>
public static class PlayerHoverPetsHealthBarPatch
{
    /// <summary>
    /// 当前鼠标悬停的玩家角色 NetId（null = 未悬停任何玩家角色）。
    /// JainaMinionBase.CanShowAttackIntent 据此决定随从攻击意图是否显示。
    /// </summary>
    public static ulong? HoveredPlayerNetId;

    [HarmonyPatch(typeof(NCreature), "_Ready")]
    private static class ReadyPostfix
    {
        private static void Postfix(NCreature __instance)
        {
            try
            {
                var creature = __instance.Entity;
                if (creature?.Player == null || __instance.Hitbox == null)
                {
                    return;
                }
                var player = creature.Player;
                __instance.Hitbox.MouseEntered += () =>
                {
                    HoveredPlayerNetId = player.NetId;
                    SetPetsHealthBarsVisible(player, true);
                    RefreshPetsIntents(player);
                };
                __instance.Hitbox.MouseExited += () =>
                {
                    if (HoveredPlayerNetId == player.NetId)
                    {
                        HoveredPlayerNetId = null;
                    }
                    SetPetsHealthBarsVisible(player, false);
                    RefreshPetsIntents(player);
                };
            }
            catch
            {
                // 悬停挂载失败不影响战斗
            }
        }
    }

    /// <summary>
    /// 刷新该玩家所有 Jaina 随从的攻击意图显示（悬停状态变化后意图出现/消失）。
    /// 自己的随从意图常驻显示，跳过（CanShowAttackIntent 自行判断）。
    /// </summary>
    private static void RefreshPetsIntents(Player player)
    {
        try
        {
            if (player?.PlayerCombatState == null || NCombatRoom.Instance == null)
            {
                return;
            }
            // 自己的随从意图常驻，悬停控制只作用于队友的随从
            if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(player))
            {
                return;
            }
            // 队友角色自身的攻击意图（武器攻击力）也在悬停时刷新
            jaina.Scripts.Character.Weapons.PlayerAttackIntentPatch.Refresh(player.Creature);
            foreach (var pet in player.PlayerCombatState.Pets.ToList())
            {
                if (pet == null || !pet.IsAlive)
                {
                    continue;
                }
                if (pet.Monster is not jaina.Scripts.Character.Minions.JainaMinionBase)
                {
                    continue;
                }
                var node = NCombatRoom.Instance.GetCreatureNode(pet);
                if (node != null)
                {
                    _ = node.RefreshIntents();
                }
            }
        }
        catch
        {
        }
    }

    private static void SetPetsHealthBarsVisible(Player player, bool visible)
    {
        try
        {
            if (player?.PlayerCombatState == null || NCombatRoom.Instance == null)
            {
                return;
            }
            // 自己的随从血条常驻，悬停控制只作用于队友的随从
            if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(player))
            {
                return;
            }
            foreach (var pet in player.PlayerCombatState.Pets.ToList())
            {
                if (pet == null || !pet.IsAlive)
                {
                    continue;
                }
                // 只控制 Jaina 随从（原版宠物如奥斯提血条常驻不受影响）
                if (pet.Monster is not jaina.Scripts.Character.Minions.JainaMinionBase)
                {
                    continue;
                }
                var node = NCombatRoom.Instance.GetCreatureNode(pet);
                if (node != null)
                {
                    node.ToggleIsInteractable(visible);
                }
            }
        }
        catch
        {
        }
    }
}
