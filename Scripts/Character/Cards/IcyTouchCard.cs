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

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

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

        // 造成 1 点伤害（+野火英雄技能伤害加成）
        if (cardPlay.Target is { IsAlive: true } target)
        {
            var wildfire = base.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.WildfirePower>();
            var wildfireStacks = wildfire?.WildfireStacks ?? 0;
            await DamageCmd.Attack(1m + wildfireStacks)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);

            // 记录英雄技能伤害（火眼莫德雷斯战吼条件用）
            jaina.Scripts.Character.JainaCastTracker.RecordHeroPowerDamage(this, 1 + wildfireStacks);
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
        // 仅当冰霜女巫吉安娜已替换英雄技能为冰冷触摸时入手
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        if (rec.CurrentHeroPowerType != typeof(IcyTouchCard))
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
