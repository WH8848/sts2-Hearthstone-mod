using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 鼠标悬停玩家角色（自己或队友）时，显示该玩家的<b>随从血条</b>（生命值）；
/// 移出后隐藏。参考亡灵契约师奥斯提：悬停队友才能查看其随从生命值。
/// 随从平时不显示血条（JainaMinionBase.IsHealthBarVisible = false，保持场面简洁）。
/// 只控制 Jaina 随从（奥斯提等原版宠物血条常驻，不受影响）。
/// 纯本地 UI：每端独立控制，联机安全。
/// </summary>
public static class PlayerHoverPetsHealthBarPatch
{
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
                __instance.Hitbox.MouseEntered += () => SetPetsHealthBarsVisible(player, true);
                __instance.Hitbox.MouseExited += () => SetPetsHealthBarsVisible(player, false);
            }
            catch
            {
                // 悬停挂载失败不影响战斗
            }
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
