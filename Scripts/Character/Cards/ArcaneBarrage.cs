using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 灯光表演 (Lightshow) - 0费攻击牌（普通，奥术派系）。
/// 对随机敌人造成 N 次 2 点伤害（N = 2 + 升级次数），将一张升级过的"灯光表演"洗入你的弃牌堆。
/// 可无限升级，每次升级攻击次数 +1。消耗。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ArcaneBarrage : JainaSpellCardTemplate
{
    /// <summary>
    /// 无限升级 - 允许无限次升级（每次升级攻击次数 +1）
    /// </summary>
    public override int MaxUpgradeLevel => int.MaxValue;

    /// <summary>
    /// 法术牌 + 奥术派系 + 消耗（打出后从本场战斗移除）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane,
         CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Beams", 2),
        new DynamicVar("Upgraded", 1)
    ];

    public override string CustomPortraitPath => "res://assets/card_art/lightshow.png";

    public ArcaneBarrage()
        : base(0, CardType.Attack, CardRarity.Common, TargetType.None, true)
    {
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

        // 对随机敌人造成 Beams 次 2 点伤害（每次随机选一个存活可命中敌人）
        int beams = (int)this.DynamicVars["Beams"].BaseValue;
        for (int i = 0; i < beams; i++)
        {
            var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
                .Where(e => e.IsAlive && e.IsHittable)
                .ToList();
            if (enemies.Count == 0)
            {
                break;
            }
            var randomEnemy = combatState.RunState.Rng.CombatTargets.NextItem(enemies);
            if (randomEnemy == null)
            {
                break;
            }
            // Move 标记：每段伤害都触发振翅（Flutter）层数减少（IsPoweredAttack）；
            // 传 cardSource/cardPlay（蜷身等依赖 cardSource 的敌方 Power 才能触发）
            await CreatureCmd.Damage(choiceContext, [randomEnemy], 2m, ValueProp.Move, base.Owner.Creature, this, cardPlay);
        }

        // 将一张升级过的"灯光表演"洗入弃牌堆（升级等级 = 本卡当前升级等级 + 1）
        var upgraded = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, base.Owner, typeof(ArcaneBarrage), base.CurrentUpgradeLevel + 1);
        if (upgraded == null)
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(upgraded);
        // 塞入弃牌堆动画 + 弃牌堆计数刷新（生成卡没有 NCard 节点，原版 tween 流程
        // 不会为它们创建动画/触发 CardAddFinished → 这里用原版生成卡塞堆流程 + 手动刷计数）
        var results = await CardPileCmd.AddGeneratedCardsToCombat(
            [upgraded], PileType.Discard, base.Owner, CardPilePosition.Bottom);
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
        // 每次升级攻击次数 +1（UpgradeValueBy 设置 WasJustUpgraded，升级预览数值绿色高亮）
        base.DynamicVars["Beams"].UpgradeValueBy(1);
        this.DynamicVars["Upgraded"].UpgradeValueBy(1);
    }
}
