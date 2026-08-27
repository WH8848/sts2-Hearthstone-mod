using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
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
    /// 动态伤害变量（参考陨石术模式：命名变量 + 分支声明 + 基础形态声明全部变量）：
    /// 未升级 = "Damage"（强能奥术飞弹，3 点，普通 DamageVar 走原版力量/虚弱/易伤预览）；
    /// 升级（星辰能量）= "Star"（5 + 力量，起始值随后每次递减 1）。
    /// 升级分支描述引用的 "Star" 在<b>基础形态也声明</b>（占位 5）——
    /// 升级形态克隆基础形态的 CanonicalVars，变量缺失会导致整个 IfUpgraded 模板
    /// 显示为字面文本（陨石术 Blast 同为"基础声明升级变量"）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => IsUpgraded
        ? [new ComputedDamageVar("Star", 5m, ComputeStar)]
        : [new ComputedDamageVar("Star", 5m, ComputeStar),
           new DamageVar("Damage", 3m, ValueProp.Move)];

    /// <summary>
    /// 星辰能量（升级形态）伤害计算（Star 变量）：5 + 力量（起始值）。
    /// 基础（强能奥术飞弹）形态不引用 Star，返回占位 5。
    /// </summary>
    private static decimal ComputeStar(CardModel card)
    {
        if (card is not GreaterArcaneMissilesCard g || g.CurrentUpgradeLevel < 1)
        {
            return 5m;
        }
        if (card.Owner?.Creature?.CombatState != null)
        {
            return 5m + card.Owner.Creature.GetPowerAmount<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>();
        }
        return 5m;
    }

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
