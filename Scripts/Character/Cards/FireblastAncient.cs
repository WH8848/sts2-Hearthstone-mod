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
/// 0费造成1点伤害，每回合开始自动加入手牌。
/// 打出后：你的所有英雄技能具有[gold]重放1[/gold]（本局对战光环，
/// 含二级火焰冲击自身——光环使英雄技能打出时施放两次）。
/// 同样可以被灌注（灌注逻辑与火焰冲击一致）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterDustyTomeCard(typeof(jaina.Scripts.Character.Jaina))]
public sealed class FireblastAncient : JainaSpellCardTemplate
{
    /// <summary>
    /// 只能升级 1 次（升级伤害+2；MaxUpgradeLevel 不能为 0——防古老牙齿超越时
    /// 对已升级火焰冲击调 CardCmd.Upgrade 崩溃）。
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 英雄技能（悬停解释）。英雄技能不视为法术牌（不挂法术牌关键词）。
    /// 永恒（Eternal）：不可从牌库移除/变形（同火焰冲击——英雄技能是英雄自带的）。
    /// 重放由 HeroPowerReplayPower 光环统一提供（本卡不再挂 Replay 关键词——
    /// 否则自身 Replay + 光环 playCount+1 会叠加成 4 次施放）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.HeroPower, CardKeyword.Eternal];

    /// <summary>
    /// 动态伤害显示：当前伤害 = 基础 1 点（含升级加成）+ 野火加成 + 奥术增幅加成
    /// （与 OnPlay 实际结算一致；每次升级 +2 点伤害）。
    /// 用 HeroPowerDamageVar（DamageVar 子类）而非 ComputedDynamicVar：
    /// DynamicVarSet.Damage 强转 DamageVar，Computed 会导致牌库网格初始化崩溃。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HeroPowerDamageVar(1m)
    ];

    /// <summary>
    /// 卡牌原画：程序绘制的"二级火焰冲击"（火焰冲击强化版：火焰+能量环）
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/fireblast_ancient.png";

    /// <summary>
    /// 升级后卡牌名称显示 "+级别"（每次升级 +2 伤害，标记升级状态）
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

        // 你的所有英雄技能具有重放1：施加光环（幂等，本局对战持续）。
        // 光环 ModifyCardPlayCount +1 → 本卡打出时 playCount=2，OnPlayWrapper
        // 自动执行本方法两次 = 重放1（效果施放两次），无需自身 for 循环。
        if (base.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.HeroPowerReplayPower>() == null)
        {
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<jaina.Scripts.Character.Powers.HeroPowerReplayPower>(
                choiceContext, base.Owner.Creature, 1m, base.Owner.Creature, this);
        }

        // 野火：英雄技能伤害永久加成（可叠加，本局对战）；奥术增幅：英雄技能额外伤害
        var wildfire = base.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.WildfirePower>();
        var wildfireStacks = wildfire?.WildfireStacks ?? 0;
        var amplifier = base.Owner.Creature.GetPower<jaina.Scripts.Character.Powers.ArcaneAmplifierPower>();
        var amplifierBonus = amplifier?.AmplifierBonus ?? 0;
        var totalDamage = (int)(base.DynamicVars.Damage.BaseValue + wildfireStacks + amplifierBonus);

        // 目标防御：无目标时不施放（自动打出兜底，防 Targeting(null) NRE）
        if (cardPlay.Target is not { IsAlive: true } fireblastTarget)
        {
            return;
        }

        var attack = DamageCmd.Attack(totalDamage)
            .FromCard(this, cardPlay)
            .Targeting(fireblastTarget)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3");
        await attack.Execute(choiceContext);

        // 记录英雄技能实际造成伤害（含力量加成；重放两次都计——火眼莫德雷斯条件用）
        jaina.Scripts.Character.JainaCastTracker.RecordHeroPowerDamage(
            this, jaina.Scripts.Character.JainaCastTracker.SumActualDamage(attack));
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
            // 英雄技能已被英雄卡替换：不再入手二级火焰冲击（按玩家区分，联机不受队友英雄卡影响）
            var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
            if (rec.CurrentHeroPowerTypeByPlayer.TryGetValue(player.NetId, out var heroPowerType) &&
                heroPowerType != null && heroPowerType != typeof(FireblastAncient))
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
        // 升级伤害 +2（UpgradeValueBy 设置 WasJustUpgraded，升级预览数值绿色高亮；
        // BaseValue 随升级增长，OnPlay 与 HeroPowerDamageVar 显示均自动跟随）。
        // 只能升级 1 次（MaxUpgradeLevel=1 亦防古老牙齿超越崩溃——MaxUpgradeLevel=0
        // 时对已升级初始卡调 Upgrade 会抛异常）。升级级别显示在标题（+级别）。
        base.DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
