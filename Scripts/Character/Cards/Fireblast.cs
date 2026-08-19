using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 火焰冲击 (Fireblast) - 吉安娜专属卡牌，只出现在初始卡组中。
/// 0费造成1点伤害，可无限升级，每回合开始自动加入手牌。
/// 使用 Basic 稀有度使其不出现战斗奖励掉落中。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterCharacterStarterCard(typeof(Jaina), 1)]
[RegisterArchaicToothTranscendence(typeof(FireblastAncient))]
public sealed class Fireblast : JainaSpellCardTemplate
{
    /// <summary>
    /// 无限升级 - 允许无限次升级
    /// </summary>
    public override int MaxUpgradeLevel => int.MaxValue;

    // 英雄技能：不挂"法术牌"关键词，不被视为法术牌（不触发法术相关效果）；
    // 挂"英雄技能"关键词用于悬停解释

    /// <summary>
    /// 英雄技能关键词（悬停显示解释；不注入卡面描述）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower];

    /// <summary>
    /// 动态伤害显示：当前伤害 = 基础（1 + 升级等级）+ 灌注层数 + 野火加成
    /// （与 OnPlay 实际结算一致；非战斗中仅显示基础 + 升级）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        ModCardVars.Computed("Damage", 1m, card =>
        {
            if (card is not Fireblast fireblast)
            {
                return 1m;
            }
            // 基础伤害：1 + 升级等级（OnUpgrade 每次升级 +1，存于 BaseValue）
            var baseDamage = fireblast.DynamicVars.Damage.BaseValue;
            if (card.Owner?.Creature?.CombatState == null)
            {
                return baseDamage;
            }
            // 灌注/野火：战斗内实时加成（与 OnPlay 结算一致）
            var empower = card.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.EmpowerPower>();
            var wildfire = card.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.WildfirePower>();
            return baseDamage + (empower?.EmpowerStacks ?? 0) + (wildfire?.WildfireStacks ?? 0);
        })
    ];

    /// <summary>
    /// 卡牌原画：炉石传说法师英雄技能"火焰冲击"高清原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/fireblast.png";

    /// <summary>
    /// 升级后卡牌名称变为"火焰冲击+1"（每级 +1 伤害）
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
            return title.GetFormattedText() + "+" + CurrentUpgradeLevel;
        }
    }

    public Fireblast()
        : base(0, CardType.Attack, CardRarity.Basic, JainaTargetTypes.AnyTargetable, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 灌注：每一层灌注增加一点英雄技能伤害；灌注后伤害从 n*1（高伤单段）
        // 变为 1*n（1 伤多段），段数 = 总伤害
        var empower = base.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.EmpowerPower>();
        var empowerStacks = empower?.EmpowerStacks ?? 0;
        // 野火：英雄技能伤害永久加成（可叠加，本局对战）
        var wildfire = base.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.WildfirePower>();
        var wildfireStacks = wildfire?.WildfireStacks ?? 0;
        var totalDamage = (int)(base.DynamicVars.Damage.BaseValue + empowerStacks + wildfireStacks);

        // 灌注：每一层灌注额外召唤一个 1/1 的小精灵（先召唤，再造成伤害）
        for (int i = 0; i < empowerStacks; i++)
        {
            await jaina.Scripts.Character.Minions.JainaMinionPool.SummonMinion<jaina.Scripts.Character.Minions.ImpMinion>(
                choiceContext, base.Owner, maxHp: 1m, attack: 1m);
        }

        // 目标防御：无目标时不施放（自动打出兜底，防 Targeting(null) NRE）
        if (cardPlay.Target is not { IsAlive: true } fireblastTarget)
        {
            return;
        }

        if (empowerStacks <= 0)
        {
            // 无灌注：单段总伤害
            await DamageCmd.Attack(totalDamage)
                .FromCard(this, cardPlay)
                .Targeting(fireblastTarget)
                .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
                .Execute(choiceContext);
        }
        else
        {
            // 灌注：1*n 多段攻击（每段 1 点伤害，段数 = 总伤害）
            for (int i = 0; i < totalDamage; i++)
            {
                await DamageCmd.Attack(1m)
                    .FromCard(this, cardPlay)
                    .Targeting(fireblastTarget)
                    .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
                    .Execute(choiceContext);
            }
        }

        // 记录英雄技能伤害（火眼莫德雷斯战吼条件用）
        jaina.Scripts.Character.JainaCastTracker.RecordHeroPowerDamage(this, totalDamage);
    }

    /// <summary>
    /// 每回合开始自动加入手牌。英雄技能卡不占手牌位：
    /// 满手（10 张普通卡）时也直接入手（CardPileCmd.Add 满手判定已豁免英雄技能卡）。
    /// 打出英雄卡（魔导师晨拥）替换英雄技能后，本卡不再自动入手（由新英雄技能接手）。
    /// </summary>
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == base.Owner)
        {
            // 英雄技能已被英雄卡替换：不再入手火焰冲击（按玩家区分，联机不受队友英雄卡影响）
            var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
            if (rec.CurrentHeroPowerTypeByPlayer.TryGetValue(player.NetId, out var heroPowerType) &&
                heroPowerType != null && heroPowerType != typeof(Fireblast))
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

    protected override void OnUpgrade()
    {
        // 每次升级伤害 +1
        base.DynamicVars.Damage.UpgradeValueBy(1m);
    }
}