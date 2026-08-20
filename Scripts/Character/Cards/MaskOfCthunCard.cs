using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 克苏恩面具 (Mask of C'Thun) - 2费攻击牌（普通，暗影派系）。
/// 造成 10 点伤害，随机分配到所有敌人身上。
/// 升级后变为夕阳漫射 (Sunset Volley)：造成 10 点伤害随机分配，并随机召唤一个费用消耗为 3 的随从（火焰派系）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MaskOfCthunCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 派系（未升级暗影 / 升级后火焰）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Fire]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Shadow];

    /// <summary>
    /// 动态伤害变量（STS2 原版机制：指向目标时 {Damage} 预览实际伤害，含力量/虚弱/易伤）。
    /// 总伤害 10 点随机分配（逐点结算，力量加成每点生效）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(10m, ValueProp.Move)];

    /// <summary>
    /// 卡牌原画：克苏恩面具 / 升级后（夕阳漫射）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/sunset_volley.png" : "res://assets/card_art/mask_of_cthun.png";

    public MaskOfCthunCard()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级为夕阳漫射：派系从暗影切换为火焰。
    /// 需显式移除/加入：LocalKeywords 懒初始化只算一次，升级形态 Keywords 缓存自基础状态。
    /// </summary>
    protected override void OnUpgrade()
    {
        RemoveKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Shadow);
        AddKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Fire);
    }

    /// <summary>
    /// 升级后卡牌名称变为"夕阳漫射 (Sunset Volley)"
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

        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 造成 10 点伤害，随机分配到所有敌人身上（逐点随机，重复命中允许）。
        // 力量只加一次：总伤害 = 10 + 力量（与卡面 {Damage} 预览一致）——
        // 第一点吃力量加成，其余各 1 点（避免逐点都加力量导致实际
        // 10×(1+力量) 超出卡面显示）。
        int strength = base.Owner.Creature.GetPowerAmount<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>();
        var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
            .Where(e => e.IsAlive && e.IsHittable)
            .ToList();
        if (enemies.Count > 0)
        {
            for (int i = 0; i < 10; i++)
            {
                var target = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
                if (target == null)
                {
                    break;
                }
                // 走 AttackCommand（DamageCmd.Attack）：触发"被攻击命中"类效果（如胆小）
                decimal damage = i == 0 ? 1m + strength : 1m;
                await DamageCmd.Attack(damage).FromCard(this, cardPlay).Targeting(target).Execute(choiceContext);
            }
        }

        // 升级后：随机召唤一个费用消耗为 3 的随从
        if (IsUpgraded)
        {
            var summoned = await JainaMinionPool.SummonRandomMinionOfCost(choiceContext, base.Owner, 3);
            if (summoned?.Monster is jaina.Scripts.Character.Minions.AntonidasMinion ant)
            {
                ant.SetSummonSourceCard(this);
            }
        }
    }
}
