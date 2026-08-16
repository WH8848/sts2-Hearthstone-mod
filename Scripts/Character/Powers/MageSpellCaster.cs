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
/// 魔法智慧之球：固定六张有用法师法术（火球术/寒冰箭/烈焰风暴/暴风雪/法术反制/寒冰护盾，
/// 卡面具体写明这六张，不展开升级形态）。
/// 终极索兰莉安：从吉安娜全部法术牌池（含升级形态）随机施放。
/// </summary>
public static class MageSpellCaster
{
    /// <summary>
    /// 魔法智慧之球固定池（类型 + 升级级别；
    /// 烈焰风暴/暴风雪/法术反制分别为火焰结界/死神之躯/异议的升级形态）
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
    /// 吉安娜全部法术牌池（与匣中古神随机施放池一致；终极索兰莉安用，
    /// 按可升级级别展开——未升级与升级形态都可能被施放）
    /// </summary>
    private static readonly Type[] AllMageSpellTypes =
    [
        typeof(Fireball),
        typeof(Frostbolt),
        typeof(ArcaneIntellect),
        typeof(FreezingPotion),
        typeof(IceBarrier),
        typeof(Trick),
        typeof(Awaken),
        typeof(NorgannonWisdom),
        typeof(DeepFreezeCard),
        typeof(FlameWard),
        typeof(DeathborneCard),
        typeof(FrostNova),
        typeof(ArcaneBarrage),
        typeof(ApexisBlast),
        typeof(IgniteCard)
    ];

    /// <summary>
    /// 随机施放一个有用的法师法术（魔法智慧之球固定六张池：
    /// 免费自动打出，随机目标；单目标法术优先选敌人）。
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

        await AutoPlayRandomly(choiceContext, player, card, preferEnemies);
    }

    /// <summary>
    /// 随机施放一个法师法术（终极索兰莉安用：从吉安娜全部法术牌池
    /// 按可升级级别展开，未升级与升级形态都可能被施放）。
    /// </summary>
    public static async Task CastRandomMageSpellFromAll(PlayerChoiceContext choiceContext, Player player, bool preferEnemies = true)
    {
        var combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rng = player.RunState.Rng.CombatCardSelection;
        var type = rng.NextItem(AllMageSpellTypes);
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
        int maxLevel = Math.Min(canonical.MaxUpgradeLevel, 2);
        int upgradeLevel = rng.NextInt(0, maxLevel + 1);
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
    /// 免费自动打出（随机目标；preferEnemies 时单目标法术优先选敌人）
    /// </summary>
    private static async Task AutoPlayRandomly(PlayerChoiceContext choiceContext, Player player, CardModel card, bool preferEnemies)
    {
        var combatState = player.Creature.CombatState;
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
