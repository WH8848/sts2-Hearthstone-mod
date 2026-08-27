using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 强能奥术飞弹 (Greater Arcane Missiles) - 1费攻击牌（稀有，火焰派系）。
/// 对随机敌人造成 3 次 3 点伤害。
/// 升级后变为"星辰能量 (Star Power)"（奥术派系）：随机对一个敌方造成 5 点伤害。
/// 重复此效果，每次伤害减少 1 点。星辰能量吃力量（起始伤害 = 5 + 力量）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class GreaterArcaneMissilesCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 可升级（升级后变为星辰能量）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 关键词：基础版 法术 + 奥术；升级版（星辰能量）法术 + 奥术
    /// （强能奥术飞弹是奥术派系，不是火焰）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Spell, JainaKeywords.Arcane];

    /// <summary>
    /// 动态伤害变量（STS2 原版机制：指向目标时 {Damage} 预览实际伤害，含力量/虚弱/易伤）：
    /// 基础 = 3；升级（星辰能量）= 5 + 力量（起始值，随后每次递减 1）。
    /// 注意：用<b>单一 Computed 变量</b>（lambda 内按 IsUpgraded 分支）而非
    /// "IsUpgraded ? [Computed(5m)] : [DamageVar(3m)]"——升级形态 clone 基础形态的
    /// DynamicVars（CanonicalVars 不会为升级形态重新求值），分支声明会导致升级形态
    /// 的 Damage 仍是基础值 3（实测：卡面显示 3 而实际伤害从 5 开始）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        // ComputedDamageVar（DamageVar 子类）："Damage" 槽强转 DamageVar 安全——
        // 用 RitsuLib ComputedDynamicVar 放 "Damage" 槽会在打出/附魔/牌库网格
        // 访问 DynamicVars.Damage 时抛 InvalidCastException（卡打出无伤害进弃牌堆）。
        new ComputedDamageVar(3m, card =>
        {
            // 升级（星辰能量）：5 + 力量（起始值）；基础（强能奥术飞弹）：3
            // 用 CurrentUpgradeLevel 分支（分支声明不会为升级形态重新求值；
            // 不按 IsMutable 早退——升级预览等克隆/不可变场景也能正确显示 5）
            if (card is not GreaterArcaneMissilesCard g)
            {
                return 3m;
            }
            if (g.CurrentUpgradeLevel >= 1)
            {
                if (card.Owner?.Creature?.CombatState != null)
                {
                    return 5m + card.Owner.Creature.GetPowerAmount<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>();
                }
                return 5m;
            }
            return 3m;
        })
    ];

    /// <summary>
    /// 卡牌原画：强能奥术飞弹 / 升级后（星辰能量）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/star_power.png" : "res://assets/card_art/greater_arcane_missiles.png";

    public GreaterArcaneMissilesCard()
        : base(1, CardType.Attack, CardRarity.Rare, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"星辰能量"
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
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    /// <summary>
    /// 升级：派系火焰 -> 奥术（LocalKeywords 懒初始化只算一次，
    /// 升级形态 Keywords 缓存自基础状态——需显式切换关键词）
    /// </summary>
    protected override void OnUpgrade()
    {
        RemoveKeyword(JainaKeywords.Fire);
        AddKeyword(JainaKeywords.Arcane);
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

        if (IsUpgraded)
        {
            // 星辰能量：随机对一个敌方造成 (5+力量) 点伤害，重复此效果每次伤害减少 1 点（直到 1）。
            // 力量只加在起始值上（1 点力量 → 6、5、4、3、2、1；10 点力量 → 15、…、1）。
            int strength = base.Owner.Creature.GetPowerAmount<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>();
            for (int damage = 5 + strength; damage >= 1; damage--)
            {
                var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
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
                // 走 AttackCommand（DamageCmd.Attack）：触发"被攻击命中"类效果（如胆小）
                await DamageCmd.Attack(damage).FromCard(this, cardPlay).Targeting(target).Execute(choiceContext);
            }
            return;
        }

        // 强能奥术飞弹：对随机敌人造成 3 次 3 点伤害
        for (int i = 0; i < 3; i++)
        {
            var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
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
            // 用索引器读取（ComputedDynamicVar 不可强转 DamageVar——
            // DynamicVars.Damage 会抛 InvalidCastException，卡打出即崩、无伤害进弃牌堆）
            await DamageCmd.Attack(base.DynamicVars["Damage"].BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
        }
    }
}
