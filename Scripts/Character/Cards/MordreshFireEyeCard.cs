using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 火眼莫德雷斯 (Mordresh Fire Eye) - 3费攻击牌（稀有，火焰派系）。
/// 在本局对战中，如果你用你的英雄技能累计造成了10点伤害，
/// 则对随机敌人造成8次10点伤害。
/// 升级后添加"保留"（回合结束时留在手牌）。
/// 条件触发发光：英雄技能本局累计伤害达到 10 时，手牌中的本卡深白描边发光
/// （提示玩家现在打出可触发效果，见 <see cref="IJainaConditionGlowCard"/>）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MordreshFireEyeCard : JainaSpellCardTemplate, IJainaConditionGlowCard
{
    /// <summary>
    /// 只能升级 1 次（升级添加"保留"）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 攻击标签（CardTag.Strike）：与"打击"类效果联动
    /// </summary>
    protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };

    /// <summary>
    /// 无关键词（攻击牌：不挂"法术牌"关键词——卡面分类仅显示"攻击"，非法术牌，
    /// 不进法术池/减费/复制类效果；原版火眼无派系）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    /// <summary>
    /// 动态伤害变量（STS2 原版机制：{Damage} 预览实际伤害，含力量/虚弱/易伤）
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(10m, ValueProp.Move)];

    /// <summary>
    /// 卡牌原画：炉石传说"火眼莫德雷斯"（Mordresh Fire Eye, BAR_547）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/mordresh_fire_eye.png";

    public MordreshFireEyeCard()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称显示 "+级别"（升级添加保留，标记升级状态）
    /// </summary>
    

    /// <summary>
    /// 发光条件：英雄技能本局累计造成伤害 ≥ 10（与效果触发条件一致）。
    /// 纯本地 UI 判定：HeroPowerDamageDealtByPlayer 两端确定性同步，联机安全。
    /// </summary>
    public bool IsGlowConditionMet(CardModel card, PlayerCombatState pcs)
    {
        var combatState = card.CombatState ?? card.Owner?.Creature?.CombatState;
        if (combatState == null || card.Owner == null)
        {
            return false;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        rec.HeroPowerDamageDealtByPlayer.TryGetValue(card.Owner.NetId, out var heroPowerDamage);
        return heroPowerDamage >= 10;
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
        // 条件：英雄技能本局累计造成 10 点伤害（火焰冲击记录）
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        rec.HeroPowerDamageDealtByPlayer.TryGetValue(base.Owner.NetId, out var heroPowerDamage);
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[Jaina] Mordresh Fire Eye: heroPowerDamage={heroPowerDamage} need>=10 -> {(heroPowerDamage >= 10 ? "proceed" : "SKIP no damage")}");
        if (heroPowerDamage < 10)
        {
            return;
        }

        // 对随机敌人造成 8 次 10 点伤害（每次独立随机目标，可重叠；
        // 目标死亡后剔除；每次命中吃力量加成，与多次攻击牌一致）
        var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
            .Where(e => e != null && e.IsAlive && e.IsHittable)
            .ToList();
        MegaCrit.Sts2.Core.Logging.Log.Info($"[Jaina] Mordresh Fire Eye: enemies={enemies.Count}");
        for (int i = 0; i < 8; i++)
        {
            enemies = enemies.Where(e => e.IsAlive).ToList();
            if (enemies.Count == 0)
            {
                break;
            }
            var target = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
            if (target == null)
            {
                break;
            }
            await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[Jaina] Mordresh Fire Eye hit #{i + 1}: {target.Name} alive={target.IsAlive}");
        }
    }

    protected override void OnUpgrade()
    {
        // 升级：添加"保留"（回合结束时留在手牌；原生关键词自动渲染词条与悬停解释）
        AddKeyword(CardKeyword.Retain);
    }
}
