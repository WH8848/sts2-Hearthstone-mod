using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 小精灵的祝福 (Blessing of the Imps) - 0费攻击牌（衍生，英雄技能）。
/// 召唤1个小精灵。造成1点伤害，随机分配到所有敌人身上。
/// 灌注机制：第一次打出灌注卡牌（灵体采集者/小精灵驾驭者）会将英雄技能
/// 替换为本卡；此后每层灌注都会让本卡额外释放一次（释放次数 = 灌注层数，
/// 每次释放都召唤1个小精灵并造成1点随机伤害）。
/// 每回合开始自动加入手牌（英雄技能卡不占手牌位）。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class BlessingOfImpsCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 英雄技能关键词（悬停解释；不注入卡面描述）——英雄技能不视为法术牌。
    /// 带 HeroPower 关键词 → 自动被随机施放/发现池排除（HeroPowerHandHelper）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.HeroPower];

    /// <summary>
    /// 动态伤害显示：每次伤害 = 1(基础) + 野火层数 + 奥术增幅(与 OnPlay 结算一致)。
    /// 用 ComputedDamageVar(DamageVar 子类,强转安全);canonical(图鉴)显示基础 1。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ComputedDamageVar(1m, card =>
        {
            if (card == null || !card.IsMutable)
            {
                return 1m;
            }
            var creature = card.Owner?.Creature;
            if (creature == null)
            {
                return 1m;
            }
            var wildfire = creature.GetPower<WildfirePower>();
            var amplifier = creature.GetPower<ArcaneAmplifierPower>();
            return 1m + (wildfire?.WildfireStacks ?? 0) + (amplifier?.AmplifierBonus ?? 0);
        })
    ];

    /// <summary>
    /// 衍生英雄技能卡不可升级
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>
    /// 卡牌原画：程序绘制的"小精灵的祝福"（小精灵 + 金色祝福光环）
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/blessing_of_imps.png";

    /// <summary>
    /// 卡名不变
    /// </summary>
    

    public BlessingOfImpsCard()
        : base(0, CardType.Attack, CardRarity.Token, TargetType.None, true)
    {
    }

    /// <summary>
    /// 效果：释放次数 = 灌注层数（至少 1 次）。每次释放：
    /// 召唤 1 个 1/1 小精灵 + 对随机敌人造成 1 点伤害（随机分配到所有敌人身上）。
    /// 灌注层数 = EmpowerPower 层数（第一次灌注替换为本卡，此后每层 +1 次释放）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪；英雄技能卡在 RecordPlayed 内被排除）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var owner = base.Owner;
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 释放次数 = 灌注层数（每层灌注额外释放一次；至少 1 次防御）
        var empower = owner.Creature.GetPower<EmpowerPower>();
        int casts = Math.Max(1, empower?.EmpowerStacks ?? 0);
        // 野火：英雄技能伤害永久加成（与火焰冲击等英雄技能卡一致）；
        // 奥术增幅：英雄技能额外伤害（每次释放的伤害 = 1 + 野火 + 增幅）
        var wildfire = owner.Creature.GetPower<WildfirePower>();
        var amplifier = owner.Creature.GetPower<ArcaneAmplifierPower>();
        int hitDamage = 1 + (wildfire?.WildfireStacks ?? 0) + (amplifier?.AmplifierBonus ?? 0);

        for (int i = 0; i < casts; i++)
        {
            // 召唤 1 个 1/1 小精灵
            await JainaMinionPool.SummonMinion<ImpMinion>(
                choiceContext, owner, maxHp: 1m, attack: 1m);

            // 造成伤害，随机分配到所有敌人身上（随机选一个可命中敌人）
            var enemies = combatState.GetOpponentsOf(owner.Creature)
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
            var attack = DamageCmd.Attack(hitDamage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3");
            await attack.Execute(choiceContext);

            // 记录英雄技能实际造成伤害（含力量加成；每次释放都计——火眼莫德雷斯条件用）
            jaina.Scripts.Character.JainaCastTracker.RecordHeroPowerDamage(
                this, jaina.Scripts.Character.JainaCastTracker.SumActualDamage(attack));
        }
    }

    /// <summary>
    /// 每回合开始自动加入手牌（仅当灌注已把英雄技能替换为本卡时）。
    /// 英雄技能卡不占手牌位：满手也直接入手。
    /// </summary>
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != base.Owner)
        {
            return;
        }
        // 仅当灌注已替换英雄技能为小精灵的祝福时入手（按玩家区分）
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        if (!rec.CurrentHeroPowerTypeByPlayer.TryGetValue(player.NetId, out var heroPowerType) ||
            heroPowerType != typeof(BlessingOfImpsCard))
        {
            return;
        }
        CardPile? pile = base.Pile;
        if (pile == null || pile.Type != PileType.Hand)
        {
            await CardPileCmd.Add(this, PileType.Hand);
        }
    }
}
