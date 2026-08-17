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
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 火焰结界 (Flame Ward) - 吉安娜专属攻击牌（罕见，2 费）。
/// 受到攻击时，对敌人造成 7 次 3 点伤害，随机分配到所有敌人身上。
/// 升级后变为"火热促销 (Fire Sale)"（1 费）：交易。对敌人造成 7 次 3 点伤害，
/// 随机分配到所有敌人身上。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class FlameWard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌；火焰派系；
    /// 升级版（火热促销）额外带交易关键词
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell,
           jaina.Scripts.Character.Keywords.JainaKeywords.Fire,
           jaina.Scripts.Character.Keywords.JainaKeywords.Tradeable]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell,
           jaina.Scripts.Character.Keywords.JainaKeywords.Fire];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move)
    ];

    /// <summary>
    /// 卡牌原画：火焰结界 / 升级后（火热促销）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/fire_sale.png" : "res://assets/card_art/flame_ward.png";

    public FlameWard()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"火热促销 (Fire Sale)"，费用 2 -> 1
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

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        if (IsUpgraded)
        {
            // 火热促销：立即造成 7 次伤害，随机分配到所有敌人
            await DealRandomDamage(choiceContext, cardPlay);
        }
        else
        {
            // 火焰结界：挂一次性结界（受击时触发 7 次随机伤害）
            await PowerCmd.Apply<FlameWardPower>(
                choiceContext, [base.Owner.Creature], base.DynamicVars.Damage.BaseValue, base.Owner.Creature, this);
        }
    }

    /// <summary>
    /// 造成 7 次伤害，每次随机分配到一名存活敌人
    /// </summary>
    private async Task DealRandomDamage(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = base.Owner.Creature.CombatState;
        for (int i = 0; i < 7; i++)
        {
            var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                .Where(e => e.IsAlive && e.IsHittable)
                .ToList();
            if (enemies.Count == 0)
            {
                break;
            }
            var target = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
            // 攻击伤害：吃力量加成（与原版多次攻击牌一致，每次命中都计算力量）；
            // 传 cardSource/cardPlay（蜷身等依赖 cardSource 的敌方 Power 才能触发）
            await CreatureCmd.Damage(choiceContext, [target], base.DynamicVars.Damage.BaseValue, ValueProp.Move, base.Owner.Creature, this, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级为火热促销：费用 2 -> 1；伤害保持 3 点不变；
        // 显式加入交易关键词（LocalKeywords 懒初始化只算一次，升级形态 Keywords 缓存自基础状态）
        EnergyCost.UpgradeBy(-1);
        AddKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Tradeable);
    }
}
