using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 冰冷触摸 (Icy Touch) - 0费攻击牌（衍生，英雄技能，冰霜派系）。
/// 造成 1 点伤害；如果该英雄技能消灭了一个角色，召唤一个 3/6 的水元素。
/// 由冰霜女巫吉安娜替换英雄技能后，每回合开始自动加入手牌（英雄技能卡不占手牌位）。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class IcyTouchCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 英雄技能关键词（悬停解释；不挂法术/派系关键词——英雄技能不算法术牌，
    /// 不触发法术相关效果，与火焰冲击/奥术爆裂一致）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower];

    /// <summary>
    /// 动态伤害变量（STS2 原版机制：指向目标时 {Damage} 预览实际伤害）：
    /// 当前伤害 = 1 + 替换继承的升级伤害 + 野火加成（与 OnPlay 实际结算一致）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ComputedDamageVar(1m, card =>
        {
            // canonical（图鉴渲染等）不可变：访问 Owner 会抛异常，直接返回基础值
            if (card == null || !card.IsMutable || card.Owner?.Creature?.CombatState == null)
            {
                return 1m;
            }
            var combatState = card.Owner.Creature.CombatState;
            var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
            rec.HeroPowerInheritedDamageByPlayer.TryGetValue(card.Owner.NetId, out var inherited);
            var wildfire = card.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.WildfirePower>();
            var amplifier = card.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.ArcaneAmplifierPower>();
            return 1m + inherited + (wildfire?.WildfireStacks ?? 0) + (amplifier?.AmplifierBonus ?? 0);
        })
    ];

    public override string CustomPortraitPath => "res://assets/card_art/icy_touch.png";

    /// <summary>
    /// 英雄技能卡不可升级
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    public IcyTouchCard()
        : base(0, CardType.Attack, CardRarity.Token, JainaTargetTypes.AnyTargetable, true)
    {
    }

    /// <summary>
    /// 卡名不变
    /// </summary>
    

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 野火：英雄技能伤害永久加成（可叠加，本局对战）；奥术增幅：英雄技能额外伤害；
        // 替换继承：英雄技能被替换时继承旧技能（火焰冲击等）升级伤害增量
        var wildfire = base.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.WildfirePower>();
        var wildfireStacks = wildfire?.WildfireStacks ?? 0;
        var amplifier = base.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.ArcaneAmplifierPower>();
        var amplifierBonus = amplifier?.AmplifierBonus ?? 0;
        var combatState = base.Owner.Creature.CombatState;
        var rec = combatState != null ? jaina.Scripts.Character.JainaCastTracker.For(combatState) : null;
        int inheritedDamage = 0;
        rec?.HeroPowerInheritedDamageByPlayer.TryGetValue(base.Owner.NetId, out inheritedDamage);
        var totalDamage = 1 + inheritedDamage + wildfireStacks + amplifierBonus;

        // 造成伤害（参考火焰冲击）
        if (cardPlay.Target is { IsAlive: true } target)
        {
            var attack = DamageCmd.Attack(totalDamage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt");
            await attack.Execute(choiceContext);

            // 记录英雄技能实际造成伤害（含力量加成——火眼莫德雷斯条件用）
            jaina.Scripts.Character.JainaCastTracker.RecordHeroPowerDamage(
                this, jaina.Scripts.Character.JainaCastTracker.SumActualDamage(attack));

            // 消灭了一个角色（目标因此死亡）→ 召唤一个 3/6 的水元素
            if (!target.IsAlive)
            {
                await JainaMinionPool.SummonMinionByType(
                    choiceContext,
                    base.Owner,
                    typeof(WaterElementalMinion),
                    maxHp: 6m,
                    attack: 3m,
                    source: this);
            }
        }
    }

    /// <summary>
    /// 每回合开始自动加入手牌（仅当已被英雄卡替换为当前英雄技能时）。
    /// 英雄技能卡不占手牌位：满手也直接入手。
    /// </summary>
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != base.Owner)
        {
            return;
        }
        // 仅当冰霜女巫吉安娜已替换英雄技能为冰冷触摸时入手（按玩家区分）
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        if (!rec.CurrentHeroPowerTypeByPlayer.TryGetValue(player.NetId, out var heroPowerType) ||
            heroPowerType != typeof(IcyTouchCard))
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
