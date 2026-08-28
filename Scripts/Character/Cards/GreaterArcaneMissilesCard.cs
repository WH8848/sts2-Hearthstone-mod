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
/// 重复此效果，每次伤害减少 1 点（直到 1）。力量并入序列起点（吃力量——每段 = (5+力量) 递减，
/// 如 +2 力量 → 7、6、5、4、3、2、1），力量不逐段重复加成。
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
    /// 升级（星辰能量）= "Star"（基数 5，力量/附魔由基类预览管道修正——起始显示 = 5 + 力量）。
    /// 升级分支描述引用的 "Star" 在<b>基础形态也声明</b>（占位 5）——
    /// 升级形态克隆基础形态的 CanonicalVars，变量缺失会导致整个 IfUpgraded 模板
    /// 显示为字面文本（陨石术 Blast 同为"基础声明升级变量"）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars => IsUpgraded
        ? [new ComputedDamageVar("Star", 5m, ComputeStar)]
        : [new ComputedDamageVar("Star", 5m, ComputeStar),
           new DamageVar("Damage", 3m, ValueProp.Move)];

    /// <summary>
    /// 星辰能量（升级形态）伤害变量（Star）：固定返回基数 5。
    /// 力量/附魔修正由 DamageVar 基类预览管道负责（UpdateCardPreview →
    /// Hook.ModifyDamage + 附魔），<b>不能在此叠加力量</b>——否则 +2 力量会
    /// 显示 9（基类预览已加 2，此处再加 2）。
    /// 基础（强能奥术飞弹）形态不引用 Star，返回占位 5。
    /// </summary>
    private static decimal ComputeStar(CardModel card)
    {
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
            // 力量只并入序列起点（+2 力量 → 7、6、5、4、3、2、1），每段为固定序列值——
            // 必须 Unpowered()：否则 Attack 管线会把力量/附魔再加一遍（+2 力量 → 9…3 双重加成）。
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
                // 走 AttackCommand（触发"被攻击命中"类效果，如胆小）
                await DamageCmd.Attack(damage).Unpowered()
                    .FromCard(this, cardPlay)
                    .Targeting(target)
                    .Execute(choiceContext);
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
