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
/// 死神之躯 (Deathborne) - 2费攻击牌（罕见，冰霜派系，卡面类型标签"攻击丨法术"）。
/// 对所有随从造成2点伤害（不伤害队友的随从），对敌人造成7次2点伤害。每消灭一个角色，召唤一个2/2的不稳定的骷髅。
/// 战场上放不下的骷髅会立即爆炸（对随机敌人造成 2 点伤害）。
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
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.None, true)
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
        // "所有随从" = 所有非英雄生物（我方随从 + 敌方随从）——
        // 手打不伤害队友（其它玩家）的随从：PetOwner 为其它玩家的随从排除；
        // 随机打出（如 Yogg 等随机释放）保持全范围——随机打出的卡可以攻击队友随从；
        // 同时排除玩家主身体（自己 + 队友英雄;多人联机下队友英雄也在 Creatures 中，
        // 不排除会把队友英雄当随从打,实测多人错误对队友造成伤害）。
        var victims = combatState.Creatures
            .Where(c => c != null && c.IsAlive && !c.IsPlayer && c != base.Owner.Creature)
            .Where(c => cardPlay.IsAutoPlay || c.PetOwner == null || c.PetOwner == base.Owner)
            .ToList();

        // 1) 对所有随从造成 2 点伤害（记录消灭数）
        // 走 AttackCommand（DamageCmd.Attack）：触发"被攻击命中"类效果（如胆小）；
        // AttackCommand 无自定义多目标列表 API → 逐个目标独立攻击命令（行为等价）
        int killed = 0;
        var beforeAlive = victims.Where(c => c.IsAlive).ToHashSet();
        foreach (var victim in victims)
        {
            if (!victim.IsAlive)
            {
                continue;
            }
            await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(victim)
                .Execute(choiceContext);
        }
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
            await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
            if (aliveBefore && !target.IsAlive)
            {
                killed++;
            }
        }

        // 3) 每消灭一个角色，召唤一个 2/2 的不稳定的骷髅；
        // 战场上放不下的骷髅会立即爆炸（对随机敌人造成 2 点伤害）
        for (int i = 0; i < killed; i++)
        {
            await JainaMinionPool.SummonVolatileSkeletonOrExplode(
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
            // 走 AttackCommand（DamageCmd.Attack）：触发"被攻击命中"类效果（如胆小）
            await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
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
