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
/// 造成 1 点伤害，召唤一个水元素。
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
    /// 当前伤害 = 1 + 野火加成（与 OnPlay 实际结算一致）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        STS2RitsuLib.Cards.DynamicVars.ModCardVars.Computed("Damage", 1m, card =>
        {
            // canonical（图鉴渲染等）不可变：访问 Owner 会抛异常，直接返回基础值
            if (card == null || !card.IsMutable || card.Owner?.Creature?.CombatState == null)
            {
                return 1m;
            }
            var wildfire = card.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.WildfirePower>();
            return 1m + (wildfire?.WildfireStacks ?? 0);
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
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            return title.GetFormattedText();
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 野火：英雄技能伤害永久加成（可叠加，本局对战）
        var wildfire = base.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.WildfirePower>();
        var wildfireStacks = wildfire?.WildfireStacks ?? 0;
        var totalDamage = 1 + wildfireStacks;

        // 造成伤害（参考火焰冲击）
        if (cardPlay.Target is { IsAlive: true } target)
        {
            await DamageCmd.Attack(totalDamage)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);

            // 记录英雄技能伤害（火眼莫德雷斯战吼条件用）
            jaina.Scripts.Character.JainaCastTracker.RecordHeroPowerDamage(this, totalDamage);
        }

        // 召唤一个水元素（3/6）
        await JainaMinionPool.SummonMinionByType(
            choiceContext,
            base.Owner,
            typeof(WaterElementalMinion),
            maxHp: 6m,
            attack: 3m,
            source: this);
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
