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
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 二级火焰冲击 (Fireblast II) - 吉安娜专属先古英雄技能（欧罗巴斯/古老牙齿
/// 把初始卡"火焰冲击"升级为此卡）。
/// 0费造成1点伤害，每回合开始自动加入手牌，可无限升级。
/// [gold]重放1[/gold]：打出后自动重放一次（效果执行两次）。
/// 同样可以被灌注（灌注逻辑与火焰冲击一致）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterDustyTomeCard(typeof(jaina.Scripts.Character.Jaina))]
public sealed class FireblastAncient : JainaSpellCardTemplate
{
    /// <summary>
    /// 无限升级 - 允许无限次升级
    /// </summary>
    public override int MaxUpgradeLevel => int.MaxValue;

    /// <summary>
    /// 英雄技能 + 重放（悬停解释）。英雄技能不视为法术牌（不挂法术牌关键词）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.HeroPower, JainaKeywords.Replay];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(1m, ValueProp.Move)
    ];

    /// <summary>
    /// 卡牌原画：程序绘制的"二级火焰冲击"（火焰冲击强化版：火焰+能量环）
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/fireblast_ancient.png";

    /// <summary>
    /// 升级后卡牌名称变为"二级火焰冲击+1"（每级 +1 伤害）
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

    public FireblastAncient()
        : base(0, CardType.Attack, CardRarity.Ancient, JainaTargetTypes.AnyTargetable, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 重放 1：打出后自动重放一次（效果执行两次）
        for (int replay = 0; replay < 2; replay++)
        {
            // 灌注：每一层灌注增加一点英雄技能伤害；灌注后伤害从 n*1（高伤单段）
            // 变为 1*n（1 伤多段），段数 = 总伤害
            var empower = base.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.EmpowerPower>();
            var empowerStacks = empower?.EmpowerStacks ?? 0;
            var totalDamage = (int)(base.DynamicVars.Damage.BaseValue + empowerStacks);

            // 灌注：每一层灌注额外召唤一个 1/1 的小精灵（先召唤，再造成伤害）
            for (int i = 0; i < empowerStacks; i++)
            {
                await jaina.Scripts.Character.Minions.JainaMinionPool.SummonMinion<jaina.Scripts.Character.Minions.ImpMinion>(
                    choiceContext, base.Owner, maxHp: 1m, attack: 1m);
            }

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

            // 记录英雄技能伤害（火眼莫德雷斯战吼条件用；重放两次都计）
            jaina.Scripts.Character.JainaCastTracker.RecordHeroPowerDamage(this, totalDamage);
        }
    }

    /// <summary>
    /// 每回合开始自动加入手牌。英雄技能卡不占手牌位：
    /// 满手（10 张普通卡）时也直接入手（CardPileCmd.Add 满手判定已豁免英雄技能卡）。
    /// 打出英雄卡（魔导师晨拥/冰霜女巫吉安娜）替换英雄技能后，本卡不再自动入手（由新英雄技能接手）。
    /// </summary>
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == base.Owner)
        {
            // 英雄技能已被英雄卡替换：不再入手二级火焰冲击
            var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
            if (rec.CurrentHeroPowerType != null && rec.CurrentHeroPowerType != typeof(FireblastAncient))
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
