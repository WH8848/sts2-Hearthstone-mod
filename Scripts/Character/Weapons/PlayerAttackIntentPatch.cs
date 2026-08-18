using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using jaina.Scripts.Character.Powers;

namespace jaina.Scripts.Character.Weapons;

/// <summary>
/// 玩家攻击意图：玩家角色与随从一致——拥有攻击力（武器攻击力），
/// <b>可攻击时显示等同于攻击力的攻击意图</b>。
/// patch <see cref="NCreature.UpdateIntent"/>：玩家角色（Monster == null）不走原版怪物意图
/// （原方法对 Monster == null 直接抛异常），在此拦截并注入攻击意图渲染——
/// 本地角色常驻显示；队友角色悬停其角色时才显示（与随从 CanShowAttackIntent 规则一致）。
/// 意图随"可攻击状态"动态出现/消失：装备武器/回合开始（行动点恢复）→ 出现；
/// 攻击后（行动点耗尽）/武器耐久归零 → 消失。
/// </summary>
public static class PlayerAttackIntentPatch
{
    /// <summary>
    /// 该玩家角色当前是否可显示攻击意图：
    /// 存活 + 装备武器（攻击力 &gt; 0 且耐久 &gt; 0）+ 本回合行动点未用完 + 显示规则
    /// （本地常驻 / 队友悬停，与随从一致）。
    /// </summary>
    public static bool CanShowAttackIntent(Creature creature)
    {
        if (creature == null || !creature.IsAlive || creature.Player == null)
        {
            return false;
        }
        var weapon = creature.Powers.OfType<JainaWeaponPower>().FirstOrDefault();
        if (weapon == null || weapon.Attack <= 0 || weapon.Amount <= 0)
        {
            return false;
        }
        var action = creature.Powers.OfType<JainaWeaponAttackAction>().FirstOrDefault();
        if (action == null || action.Amount <= 0)
        {
            return false;
        }
        // 与随从规则一致：本地角色常驻显示；队友角色悬停其角色时才显示
        var player = creature.Player;
        if (!LocalContext.IsMe(player) &&
            PlayerHoverPetsHealthBarPatch.HoveredPlayerNetId != player.NetId)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 立即刷新玩家角色的攻击意图显示（装备武器/攻击后/回合开始/武器消失时调用）。
    /// 纯 UI，联机两端各自渲染。
    /// </summary>
    public static void Refresh(Creature playerCreature)
    {
        if (playerCreature == null || playerCreature.CombatState == null)
        {
            return;
        }
        var node = NCombatRoom.Instance?.GetCreatureNode(playerCreature);
        if (node != null)
        {
            _ = node.UpdateIntent(playerCreature.CombatState.Players.Select(p => p.Creature));
        }
    }

    /// <summary>
    /// 玩家角色意图渲染拦截（原方法对 Monster == null 抛异常——玩家角色没有怪物状态机）
    /// </summary>
    [HarmonyPatch(typeof(NCreature), nameof(NCreature.UpdateIntent))]
    public static class PlayerIntentRenderPatch
    {
        private static bool Prefix(NCreature __instance, IEnumerable<Creature> targets)
        {
            var entity = __instance.Entity;
            if (entity == null || entity.Monster != null || entity.Player == null)
            {
                return true; // 非玩家角色（怪物/随从）：走原版路径
            }
            // 玩家角色：清空旧意图，按条件显示攻击意图（等同于武器攻击力）
            foreach (var item in __instance.IntentContainer.GetChildren().OfType<NIntent>().ToList())
            {
                __instance.IntentContainer.RemoveChildSafely(item);
                item.QueueFreeSafely();
            }
            if (!CanShowAttackIntent(entity))
            {
                return false;
            }
            var weapon = entity.Powers.OfType<JainaWeaponPower>().FirstOrDefault();
            if (weapon == null)
            {
                return false;
            }
            // 延迟读取武器攻击力（武器切换/耐久变化后意图数值即时跟随）
            var nIntent = NIntent.Create(0f);
            __instance.IntentContainer.AddChildSafely(nIntent);
            nIntent.UpdateIntent(new SingleAttackIntent(() => weapon.Attack), targets, entity);
            return false;
        }
    }
}
