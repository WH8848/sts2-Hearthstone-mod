using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 永时火焰箭 (Everfire Arrow) - 1费稀有攻击牌（火焰派系）。
/// 吸血：对一个角色造成 3 点伤害，并回复等量生命。
/// 升级后：下回合开始时，将本牌移回你的手牌。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class EverfireArrowCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 只能升级 1 次（升级添加"下回合开始回手"效果）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 攻击标签（CardTag.Strike）：与"打击"类效果联动
    /// </summary>
    protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };

    /// <summary>
    /// 法术牌 + 火焰派系 + 吸血（悬停显示关键词解释）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Spell, JainaKeywords.Fire, JainaKeywords.Lifesteal];

    /// <summary>
    /// 动态伤害变量（STS2 原版机制：指向目标时 {Damage} 预览实际伤害，含力量/虚弱/易伤）
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(3m, ValueProp.Move)];

    /// <summary>
    /// 卡牌原画：炉石传说"永时火焰箭"（Eternal Firebolt, END_025）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/eternal_firebolt.png";

    public EverfireArrowCard()
        : base(1, CardType.Attack, CardRarity.Rare, JainaTargetTypes.AnyTargetable, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称显示 "+级别"（升级添加回手效果，标记升级状态）
    /// </summary>
    

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 目标防御：自动打出（戏法图腾/浩劫等）无目标时不施放（AutoPlayTargetPatch 会补全目标，
        // 这里兜底防 NRE——卡在空中/回合循环死亡）
        if (cardPlay.Target is not { IsAlive: true } target)
        {
            return;
        }

        // 造成 3 点伤害（吃力量加成，与卡面预览一致）
        var dmg = (int)base.DynamicVars.Damage.BaseValue;
        var attack = DamageCmd.Attack(dmg)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt");
        await attack.Execute(choiceContext);

        // 吸血：造成多少伤害吸多少血（按实际造成伤害结算，含力量增伤/易伤等修正，
        // 与卡面预览的基础值不同——预览3点、实际打5点则回5点）
        var actualDamage = jaina.Scripts.Character.JainaCastTracker.SumActualDamage(attack);
        if (actualDamage > 0)
        {
            await CreatureCmd.Heal(base.Owner.Creature, actualDamage);
        }

        // 升级后：下回合开始时，将本牌移回你的手牌（挂在玩家身上的回手 Power）
        if (IsUpgraded)
        {
            var power = base.Owner.Creature.GetPower<EverfireArrowRecallPower>();
            if (power == null)
            {
                power = await PowerCmd.Apply<EverfireArrowRecallPower>(
                    choiceContext, base.Owner.Creature, 1m, base.Owner.Creature, this);
            }
            power?.TargetCards.Add(this);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级不改变数值——仅解锁"下回合开始将本牌移回你的手牌"效果
        // （IsUpgraded 分支在 OnPlay 中处理；不出牌时无副作用）
    }
}
