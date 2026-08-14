using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 冰锥术 (Cone of Cold) - 2费攻击（普通，冰霜派系）。
/// 造成 3 次 1 点伤害，随机分配到所有敌人身上，每次伤害给予敌人 1 层冻结。
/// 升级后变为"暴风雪 (Blizzard)"：造成 7 次 2 点伤害，随机分配到所有敌人身上，每次伤害给予 1 层冻结。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ConeOfCold : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 冰霜派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Frost];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(1m, ValueProp.Move)
    ];

    /// <summary>
    /// 卡牌原画：冰锥术 / 升级后（暴风雪）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/blizzard.png" : "res://assets/card_art/cone_of_cold.png";

    public ConeOfCold()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"暴风雪 (Blizzard)"
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
    /// 每次随机伤害的次数（未升级 3 次 / 升级后 7 次）
    /// </summary>
    private int HitCount => IsUpgraded ? 7 : 3;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var combatState = base.Owner.Creature.CombatState;
        for (int i = 0; i < HitCount; i++)
        {
            var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                .Where(e => e.IsAlive && e.IsHittable)
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
            // 每次伤害都会给予敌人 1 层冻结
            // 攻击伤害：吃力量加成（与原版多次攻击牌一致，每次命中都计算力量）
            await CreatureCmd.Damage(choiceContext, [target], base.DynamicVars.Damage.BaseValue, ValueProp.Move, base.Owner.Creature);
            await PowerCmd.Apply<FreezePower>(choiceContext, [target], 1m, base.Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级为暴风雪：伤害 1 -> 2（次数 3 -> 7 由 HitCount 处理；费用不变 2 费）
        base.DynamicVars.Damage.BaseValue = 2m;
    }
}
