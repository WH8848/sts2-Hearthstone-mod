using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 绿洲盟军 (Oasis Ally) - 1费技能牌（普通，冰霜派系）。
/// 受到攻击时，召唤一个 3/6 的水元素（一次性结界：触发后或下一个回合开始消失）。
/// 升级后变为霜巫十字绣 (Frost Lich Cross-Stitch)（1费，冰霜）：
/// 对一个角色造成 3 点伤害，召唤一个 3/6 的水元素。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class OasisAllyCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 只能升级 1 次（升级变为霜巫十字绣）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 法术牌 + 冰霜派系（升级后不变）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Spell, JainaKeywords.Frost];

    /// <summary>
    /// 动态伤害变量（"Damage"）：升级版（霜巫十字绣）造成 3 点基础伤害，
    /// 走原版力量/附魔预览（{Damage:diff()} 动态显示，与寒冰箭同模式）。
    /// 基础形态也声明（IfUpgraded 模板全分支解析，变量缺失会显示字面文本）。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar("Damage", 3m, ValueProp.Move)];

    /// <summary>
    /// 卡牌原画：绿洲盟军 / 升级后（霜巫十字绣 Frost Lich Cross-Stitch）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/frost_lich_cross_stitch.png" : "res://assets/card_art/oasis_ally.png";

    /// <summary>
    /// 悬停提示：显示召唤的衍生物"水元素"卡（同深度冻结做法）
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        new CardHoverTip(ModelDb.Card<WaterElementalCard>())
    ];

    /// <summary>
    /// 目标：升级（霜巫十字绣）选择任意角色（造成 3 点伤害）；基础（绿洲盟军）无目标，挂结界。
    /// </summary>
    public override TargetType TargetType =>
        IsUpgraded ? JainaTargetTypes.AnyTargetable : TargetType.None;

    public OasisAllyCard()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"霜巫十字绣 (Frost Lich Cross-Stitch)"
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
            // 霜巫十字绣：对一个角色造成 3 点伤害（吃力量/附魔——卡面 {Damage:diff()} 动态显示），召唤一个 3/6 的水元素
            if (cardPlay.Target is not { IsAlive: true } target)
            {
                return;
            }
            await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
            await JainaMinionPool.SummonMinionByType(
                choiceContext, base.Owner, typeof(WaterElementalMinion), maxHp: 6, attack: 3);
            return;
        }

        // 绿洲盟军：挂一次性结界（受到攻击时召唤一个 3/6 水元素；触发后或下回合开始消失）
        await PowerCmd.Apply<OasisAllyPower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
