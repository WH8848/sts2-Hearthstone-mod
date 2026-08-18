using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Minions;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 死神之躯 (Deathborne) - 2费技能牌（罕见，冰霜派系）。
/// 对所有随从造成2点伤害，对敌人造成7次2点伤害。每消灭一个角色，召唤一个2/2的不稳定的骷髅。
/// 升级后变为"暴风雪 (Blizzard)"：造成7次2点伤害，随机分配到所有敌人身上。给予敌方全体7层冻结。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class DeathborneCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 冰霜派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Frost];

    /// <summary>
    /// 动态伤害变量（STS2 原版机制：指向目标时 {Damage} 预览实际伤害，含力量/虚弱/易伤）
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(2m, ValueProp.Move)];

    /// <summary>
    /// 卡牌原画：死神之躯 / 升级后（暴风雪）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/blizzard.png" : "res://assets/card_art/deathborne.png";

    public DeathborneCard()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"暴风雪 (Blizzard)"
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    /// <summary>
    /// 悬停提示：显示每消灭一个角色召唤的衍生物"不稳定的骷髅"卡
    /// （参考灵体采集者显示小精灵/冰霜女巫吉安娜显示水元素的做法）。
    /// 升级后（暴风雪）不再召唤骷髅，不显示衍生卡悬停。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            if (!IsUpgraded)
            {
                yield return new CardHoverTip(MegaCrit.Sts2.Core.Models.ModelDb.Card<VolatileSkeletonCard>());
            }
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        if (IsUpgraded)
        {
            // 暴风雪：造成 7 次 2 点伤害，随机分配到所有敌人身上，每次伤害给予 1 层冻结
            await PlayAsBlizzard(choiceContext, cardPlay, combatState);
            return;
        }

        // 死神之躯：对所有随从造成 2 点伤害；对敌人造成 7 次 2 点伤害；
        // 每消灭一个角色，召唤一个 2/2 的不稳定的骷髅。
        // "所有随从" = 场上所有非英雄生物（我方随从 + 敌方随从）。
        var victims = combatState.Creatures
            .Where(c => c != null && c.IsAlive && c != base.Owner.Creature)
            .ToList();

        // 1) 对所有随从造成 2 点伤害（记录消灭数）
        int killed = 0;
        var beforeAlive = victims.Where(c => c.IsAlive).ToHashSet();
        await CreatureCmd.Damage(choiceContext, victims, base.DynamicVars.Damage.BaseValue,
            ValueProp.Move, base.Owner.Creature, this, cardPlay);
        killed += beforeAlive.Count(c => !c.IsAlive);

        // 2) 对敌人造成 7 次 2 点伤害（随机分配到所有敌人，每次伤害都检查消灭）
        var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
            .Where(e => e != null && e.IsAlive && e.IsHittable)
            .ToList();
        for (int i = 0; i < 7; i++)
        {
            enemies = enemies.Where(e => e.IsAlive).ToList();
            if (enemies.Count == 0)
            {
                break;
            }
            var target = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
            if (target == null)
            {
                break;
            }
            bool aliveBefore = target.IsAlive;
            await CreatureCmd.Damage(choiceContext, [target], base.DynamicVars.Damage.BaseValue,
                ValueProp.Move, base.Owner.Creature, this, cardPlay);
            if (aliveBefore && !target.IsAlive)
            {
                killed++;
            }
        }

        // 3) 每消灭一个角色，召唤一个 2/2 的不稳定的骷髅
        for (int i = 0; i < killed; i++)
        {
            await JainaMinionPool.SummonMinion<VolatileSkeleton>(
                choiceContext, base.Owner, maxHp: 2m, attack: 2m);
        }
    }

    /// <summary>
    /// 暴风雪（升级形态）：造成 7 次 2 点伤害，随机分配到所有敌人身上；给予敌方全体 7 层冻结。
    /// </summary>
    private async Task PlayAsBlizzard(PlayerChoiceContext choiceContext, CardPlay cardPlay, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        for (int i = 0; i < 7; i++)
        {
            var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                .Where(e => e.IsAlive && e.IsHittable)
                .ToList();
            if (enemies.Count == 0)
            {
                break;
            }
            var target = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
            if (target == null)
            {
                break;
            }
            // 攻击伤害：吃力量加成（与原版多次攻击牌一致，每次命中都计算力量）；
            // 传 cardSource/cardPlay（蜷身等依赖 cardSource 的敌方 Power 才能触发）
            await CreatureCmd.Damage(choiceContext, [target], base.DynamicVars.Damage.BaseValue,
                ValueProp.Move, base.Owner.Creature, this, cardPlay);
        }

        // 给予敌方全体 7 层冻结
        var allEnemies = combatState.GetOpponentsOf(base.Owner.Creature)
            .Where(e => e != null && e.IsAlive)
            .ToList();
        foreach (var enemy in allEnemies)
        {
            await PowerCmd.Apply<FreezePower>(choiceContext, [enemy], 7m, base.Owner.Creature, this);
        }
    }
}
