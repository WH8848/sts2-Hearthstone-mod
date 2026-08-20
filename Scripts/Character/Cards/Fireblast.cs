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
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 火焰冲击 (Fireblast) - 吉安娜专属卡牌，只出现在初始卡组中。
/// 0费造成1点伤害，可无限升级（每次升级伤害+1），每回合开始自动加入手牌。
/// 使用 Basic 稀有度使其不出现战斗奖励掉落中。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterCharacterStarterCard(typeof(Jaina), 1)]
[RegisterArchaicToothTranscendence(typeof(FireblastAncient))]
public sealed class Fireblast : JainaSpellCardTemplate
{
    /// <summary>
    /// 可无限升级（每次升级伤害+1；升级能力由古老牙齿超越为二级火焰冲击承接）
    /// </summary>
    public override int MaxUpgradeLevel => int.MaxValue;

    // 英雄技能：不挂"法术牌"关键词，不被视为法术牌（不触发法术相关效果）；
    // 挂"英雄技能"关键词用于悬停解释
    // 永恒（Eternal）：不可从牌库移除/变形——英雄技能是英雄自带的，
    // 防止营地移除（SlipperyBridge/Cook）、PaelsTooth 移除、WoodCarvings 变形
    // 等事件把默认英雄技能永久删出牌库（IsRemovable/IsTransformable 均按 Eternal 判定）

    /// <summary>
    /// 英雄技能关键词（悬停显示解释；不注入卡面描述）+ 永恒（不可移除/变形）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower, CardKeyword.Eternal];

    /// <summary>
    /// 升级后卡牌名称显示 "+级别"（升级每次 +1 伤害，标记升级状态）
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

    /// <summary>
    /// 动态伤害显示：当前伤害 = 基础 1 点（含升级加成）+ 野火加成 + 奥术增幅加成
    /// （与 OnPlay 实际结算一致；每次升级 +1 点伤害）。
    /// 用 HeroPowerDamageVar（DamageVar 子类）而非 ComputedDynamicVar：
    /// DynamicVarSet.Damage 强转 DamageVar，Computed 会导致牌库网格初始化崩溃。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HeroPowerDamageVar(1m)
    ];

    /// <summary>
    /// 卡牌原画：炉石传说法师英雄技能"火焰冲击"高清原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/fireblast.png";

    public Fireblast()
        : base(0, CardType.Attack, CardRarity.Basic, JainaTargetTypes.AnyTargetable, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

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

        await DamageCmd.Attack(totalDamage)
            .FromCard(this, cardPlay)
            .Targeting(fireblastTarget)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);

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

    /// <summary>
    /// 升级：每次升级伤害 +1（UpgradeValueBy 设置 WasJustUpgraded，升级预览数值绿色高亮；
    /// BaseValue 随升级增长，OnPlay 与 HeroPowerDamageVar 显示均自动跟随）
    /// </summary>
    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(1m);
    }
}