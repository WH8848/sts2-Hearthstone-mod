using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
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
public sealed class Fireblast : JainaSpellCardTemplate
{
    /// <summary>
    /// 无限升级 - 允许无限次升级
    /// </summary>
    public override int MaxUpgradeLevel => int.MaxValue;

    // 英雄技能：不挂"法术牌"关键词，不被任何衍生发现；挂"英雄技能"关键词用于悬停解释

    /// <summary>
    /// 英雄技能关键词（悬停显示解释；不注入卡面描述）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(1m, ValueProp.Move)
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
        var totalDamage = (int)(base.DynamicVars.Damage.BaseValue + empowerStacks);

        if (empowerStacks <= 0)
        {
            // 无灌注：单段总伤害
            await DamageCmd.Attack(totalDamage)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target!)
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
                    .Targeting(cardPlay.Target!)
                    .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
                    .Execute(choiceContext);
            }
        }

        // 灌注：每一层灌注额外召唤一个 1/1 的小精灵
        for (int i = 0; i < empowerStacks; i++)
        {
            await jaina.Scripts.Character.Minions.JainaMinionPool.SummonMinion<jaina.Scripts.Character.Minions.ImpMinion>(
                choiceContext, base.Owner, maxHp: 1m, attack: 1m);
        }
    }

    /// <summary>
    /// 每回合开始自动加入手牌
    /// </summary>
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == base.Owner)
        {
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