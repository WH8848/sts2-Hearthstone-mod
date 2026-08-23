using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 陨石术 (Meteor) - 3费攻击牌（普通，火焰派系）。
/// 对一个敌人造成 15 点伤害，再对随机敌人造成 2 次 4 点伤害。
/// 升级后变为"烈焰风暴 (Flamestrike)"（3 费）：造成 7 次 5 点伤害，随机分配到所有敌人身上。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MeteorCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 火焰派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Fire];

    /// <summary>
    /// 动态伤害变量（STS2 原版机制：指向目标时 {Damage} 预览实际伤害，含力量/虚弱/易伤）：
    /// 基础 = 15 点主伤害 + 2 次 4 点溅射；升级（烈焰风暴）= 7 次 5 点。
    /// 注意：Blast 变量<b>基础形态也声明</b>（值 5）——升级分支的描述 {Blast:diff()}
    /// 在基础形态的升级预览/悬停中也要能解析（变量不存在会导致整个 IfUpgraded 模板
    /// 显示为字面文本，卡面出现 {Blast:diff()} 不替换）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => IsUpgraded
        ? [new DamageVar("Blast", 5m, ValueProp.Move)]
        : [new DamageVar("Blast", 5m, ValueProp.Move),
           new DamageVar("Damage", 15m, ValueProp.Move),
           new DamageVar("Splash", 4m, ValueProp.Move)];

    /// <summary>
    /// 卡牌原画：陨石术 / 升级后（烈焰风暴）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/flamestrike.png" : "res://assets/card_art/meteor.png";

    public MeteorCard()
        : base(3, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, true)
    {
    }

    /// <summary>
    /// 升级后（烈焰风暴）无需选择目标：随机造成 7 次 5 点伤害，随机分配到所有敌人
    /// （OnPlay 循环随机目标；未升级陨石术仍为指向单体敌人）。
    /// </summary>
    public override TargetType TargetType =>
        IsUpgraded ? TargetType.None : TargetType.AnyEnemy;

    /// <summary>
    /// 升级后卡牌名称变为"烈焰风暴 (Flamestrike)"
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
            // 烈焰风暴：造成 7 次 5 点伤害，随机分配到所有敌人
            for (int i = 0; i < 7; i++)
            {
                var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                    .Where(e => e != null && e.IsAlive && e.IsHittable)
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
                // 走 AttackCommand（DamageCmd.Attack）：触发"被攻击命中"类效果（如胆小）
                await DamageCmd.Attack(base.DynamicVars["Blast"].BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(target)
                    .Execute(choiceContext);
            }
            return;
        }

        // 陨石术：对一个敌人造成 15 点伤害
        if (cardPlay.Target is { IsAlive: true } mainTarget)
        {
            await DamageCmd.Attack(base.DynamicVars["Damage"].BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(mainTarget)
                .Execute(choiceContext);
        }

        // 再对随机敌人造成 2 次 4 点伤害
        for (int i = 0; i < 2; i++)
        {
            var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                .Where(e => e != null && e.IsAlive && e.IsHittable)
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
            await DamageCmd.Attack(base.DynamicVars["Splash"].BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
        }
    }
}
