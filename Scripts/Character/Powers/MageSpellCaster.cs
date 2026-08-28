using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;
using MinionLib.Targeting;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 随机施放法师法术工具（魔法智慧之球 / 终极索兰莉安共用）。
/// 魔法智慧之球：固定五张有用法师法术（火球术/寒冰箭/烈焰风暴/暴风雪/法术反制，
/// 卡面具体写明这五张，不展开升级形态）。
/// 终极索兰莉安：从吉安娜全部法术牌池（含升级形态）随机施放。
/// </summary>
public static class MageSpellCaster
{
    /// <summary>
    /// 魔法智慧之球固定池（类型 + 升级级别；
    /// 烈焰风暴/暴风雪/异议分别为火焰结界/死神之躯/法术反制的升级形态）
    /// </summary>
    public static readonly (Type Type, int UpgradeLevel)[] UsefulMageSpells =
    [
        (typeof(Fireball), 0),
        (typeof(Frostbolt), 0),
        (typeof(FlameWard), 1),   // 烈焰风暴
        (typeof(DeathborneCard), 1), // 暴风雪
        (typeof(Objection), 0),   // 法术反制（基础形态）
    ];

    /// <summary>
    /// 随机施放一个有用的法师法术（魔法智慧之球固定五张池：
    /// 免费自动打出，随机目标；单目标法术优先选敌人）。
    /// 火焰冲击等英雄技能卡不是法术牌，绝不会被施放（池内无 + 实例过滤兜底）。
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
        // 英雄技能卡不是法术牌：实例过滤兜底（火焰冲击等绝不施放）
        if (HeroPowerHandHelper.IsHeroPowerCard(card))
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);

        await AutoPlayRandomly(choiceContext, player, card, preferEnemies);
    }

    /// <summary>
    /// 吉安娜全部法术牌池（终极索兰莉安用）：动态构建（攻击/技能牌，含升级形态，
    /// 排除英雄技能/任务线卡），取代硬编码 typeof 列表。
    /// </summary>
    public static async Task CastRandomMageSpellFromAll(PlayerChoiceContext choiceContext, Player player, bool preferEnemies = true)
    {
        var combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rng = player.RunState.Rng.CombatCardSelection;
        // 动态池：类型 + 升级级别一起随机（未升级与升级形态都可能被施放）
        var pool = jaina.Scripts.Character.JainaCastTracker.BuildAllSpellPool();
        if (pool.Count == 0)
        {
            return;
        }
        var (type, upgradeLevel) = rng.NextItem(pool);
        if (type == null)
        {
            return;
        }
        var canonical = MegaCrit.Sts2.Core.Models.ModelDb.GetByIdOrNull<MegaCrit.Sts2.Core.Models.CardModel>(
            MegaCrit.Sts2.Core.Models.ModelDb.GetId(type));
        if (canonical == null)
        {
            return;
        }
        // 英雄技能卡不是法术牌：池内过滤兜底
        if (HeroPowerHandHelper.IsHeroPowerCard(canonical))
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

        await AutoPlayRandomly(choiceContext, player, card, preferEnemies);
    }

    /// <summary>
    /// 免费自动打出（随机目标；preferEnemies 时单目标法术尽可能以敌人为目标——
    /// 有合法敌人时只从敌人里选，无敌人时回退全部合法目标，不因无敌人而放弃施放）
    /// </summary>
    private static async Task AutoPlayRandomly(PlayerChoiceContext choiceContext, Player player, CardModel card, bool preferEnemies)
    {
        // 魔法智慧之球/终极索兰莉安是吉安娜 mod 的随机释放机制（非打出触发，手打标记不适用）：
        // 显式置位"吉安娜发起"——其释放的法术触发选择自动选（不弹界面）
        AutoPlayGuard.CurrentAutoPlayIsJainaOrigin = true;
        var combatState = player.Creature.CombatState;
        Creature? target = null;
        bool isCustomSingleTarget =
            CustomTargetTypeManager.TryGetCustomTargetType(card.TargetType, out var customType) &&
            customType.IsSingleTarget;
        if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyPlayer ||
            card.TargetType == TargetType.AnyAlly || isCustomSingleTarget)
        {
            IEnumerable<Creature> pool = combatState.Creatures.Where(c => c != null && c.IsAlive && card.IsValidTarget(c));
            if (preferEnemies)
            {
                // 尽可能以敌人为目标：有合法敌人时只从敌人里选
                // （原版 AnyEnemy 与自定义单目标如 AnyTargetable 火球术/寒冰箭一致——
                // 球施放的法术优先打敌人，不打自己/队友/己方随从）；
                // 无敌人时回退全部合法目标（不因无敌人而放弃施放）
                var enemies = pool.Where(c => c.Side != player.Creature.Side).ToList();
                if (enemies.Count > 0)
                {
                    pool = enemies;
                }
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
