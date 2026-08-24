using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 点燃 (Ignite) - 1费攻击牌（普通，火焰派系）。
/// 造成 8 点伤害，将一张升级过的"点燃"洗入你的抽牌堆。
/// 可无限升级，每次升级伤害 +4。打出后进入弃牌堆（不消耗）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class IgniteCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 无限升级 - 允许无限次升级（每次升级伤害 +4）
    /// </summary>
    public override int MaxUpgradeLevel => int.MaxValue;

    /// <summary>
    /// 无限升级卡：发现/随机生成池只展开未升级形态（0 级），
    /// 只有倒带/西瓦拉等"复制实际施放过的牌"才保留真实升级层数。
    /// </summary>
    public override int DiscoverPoolMaxUpgradeLevel => 0;

    /// <summary>
    /// 法术牌 + 火焰派系（无消耗：打出的点燃进入弃牌堆，之后随弃牌堆洗回可再次抽到）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Fire];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("Upgraded", 1)
    ];

    public override string CustomPortraitPath => "res://assets/card_art/ignite.png";

    public IgniteCard()
        : base(1, CardType.Attack, CardRarity.Common, JainaTargetTypes.AnyTargetable, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"点燃+{级别}"（可无限升级，级别递增）
    /// </summary>
    

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        if (cardPlay.Target is { IsAlive: true } target)
        {
            await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .WithHitFx("vfx/vfx_attack_blunt")
                .Execute(choiceContext);
        }

        // 将一张升级过的"点燃"洗入抽牌堆（升级等级 = 本卡当前升级等级 + 1）
        var combatState = base.Owner.Creature.CombatState;
        var upgraded = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, base.Owner, typeof(IgniteCard), base.CurrentUpgradeLevel + 1);
        if (upgraded == null)
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(upgraded);
        // 塞入抽牌堆动画 + 抽牌堆计数刷新（生成卡没有 NCard 节点，原版 tween 流程
        // 不会为它们创建动画/触发 CardAddFinished → 这里用原版生成卡塞堆流程 + 手动刷计数）
        var results = await CardPileCmd.AddGeneratedCardsToCombat(
            [upgraded], PileType.Draw, base.Owner, CardPilePosition.Bottom);
        CardCmd.PreviewCardPileAdd(results, 1.0f);
        foreach (var r in results)
        {
            if (r.success)
            {
                r.cardAdded.Pile?.InvokeCardAddFinished();
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 每次升级伤害 +4（UpgradeValueBy 设置 WasJustUpgraded，升级预览数值绿色高亮）；
        // Upgraded 升级修正 +1：卡面显示值 = 基础 1 + 升级次数 = 当前级别 + 1
        // （"将一张点燃+{Upgraded}加入弃牌堆"——生成的卡等级 = 当前级别 + 1）
        base.DynamicVars.Damage.UpgradeValueBy(4m);
        this.DynamicVars["Upgraded"].UpgradeValueBy(1);
    }
}
