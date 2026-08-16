using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Cards;
using MinionLib.Targeting;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 随机施放法师法术工具（魔法智慧之球 / 终极索兰莉安共用）。
/// 有用法师法术池：火球术/寒冰箭/烈焰风暴/暴风雪/法术反制/寒冰护盾
/// （烈焰风暴/暴风雪/法术反制为升级形态）。
/// </summary>
public static class MageSpellCaster
{
    /// <summary>
    /// 有用法师法术池（类型 + 升级级别）
    /// </summary>
    public static readonly (Type Type, int UpgradeLevel)[] UsefulMageSpells =
    [
        (typeof(Fireball), 0),
        (typeof(Frostbolt), 0),
        (typeof(FlameWard), 1),   // 烈焰风暴
        (typeof(DeathborneCard), 1), // 暴风雪
        (typeof(Objection), 1),   // 法术反制
        (typeof(IceBarrier), 0),
    ];

    /// <summary>
    /// 随机施放一个有用的法师法术（免费自动打出，随机目标；单目标法术优先选敌人）。
    /// </summary>
    public static async Task CastRandomMageSpell(PlayerChoiceContext choiceContext, Player player, bool preferEnemies = true)
    {
        var combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rng = player.RunState.Rng.CombatCardSelection;
        var (type, upgradeLevel) = rng.NextItem(UsefulMageSpells);
        if (type == null)
        {
            return;
        }
        var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, player, type, upgradeLevel);
        if (card == null)
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);

        // 单目标法术：随机选合法目标（preferEnemies 时优先敌人）
        Creature? target = null;
        if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyPlayer ||
            card.TargetType == TargetType.AnyAlly ||
            (CustomTargetTypeManager.TryGetCustomTargetType(card.TargetType, out var customType) &&
             customType.IsSingleTarget))
        {
            IEnumerable<Creature> pool = combatState.Creatures.Where(c => c != null && c.IsAlive && card.IsValidTarget(c));
            if (preferEnemies && card.TargetType == TargetType.AnyEnemy)
            {
                // 尽可能以敌人为目标：只从敌人里选
                pool = pool.Where(c => c.Side != player.Creature.Side);
            }
            var candidates = pool.ToList();
            target = candidates.Count > 0 ? player.RunState.Rng.CombatTargets.NextItem(candidates) : null;
            if (target == null)
            {
                return;
            }
        }
        await CardCmd.AutoPlay(choiceContext, card, target);
    }
}
