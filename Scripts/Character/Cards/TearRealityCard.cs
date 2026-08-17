using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 撕裂现实 (Tear Reality) - 1费技能牌（罕见，奥术派系）。
/// 随机将 2 张法师法术牌置入你的手牌，其费用消耗减少 1 点。
/// 升级后变为"操控时间 (Time Control)"：发现两张奥术法术牌，其费用消耗减少 1 点。
/// "来自过去"仅为卡牌描述风味——撕裂现实检索吉安娜的全部法术牌，
/// 操控时间检索吉安娜的全部奥术法术牌（攻击/技能牌）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class TearRealityCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 奥术派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：撕裂现实 / 升级后（操控时间）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/time_control.png" : "res://assets/card_art/tear_reality.png";

    public TearRealityCard()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"操控时间 (Time Control)"
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
    /// 吉安娜全部法术牌池（攻击/技能牌，排除自身与英雄技能卡）。
    /// 与匣中古神随机施放池保持一致（YoggBoxCard.SpellTypes）。
    /// 每种法术牌的升级形态（+）也作为独立候选（升级后的法术牌可被检索）。
    /// </summary>
    private static readonly System.Type[] AllSpellTypes =
    [
        typeof(Fireball),
        typeof(Frostbolt),
        typeof(ArcaneIntellect),
        typeof(RayOfFrostCard),
        typeof(IceBarrier),
        typeof(Trick),
        typeof(Awaken),
        typeof(NorgannonWisdom),
        typeof(DeepFreezeCard),
        typeof(FlameWard),
        typeof(DeathborneCard),
        typeof(FrostNova),
        typeof(ArcaneBarrage),
        typeof(ApexisBlast),
        typeof(IgniteCard)
    ];

    /// <summary>
    /// 吉安娜全部奥术法术牌池（攻击/技能牌中挂奥术派系关键词的）。
    /// </summary>
    private static readonly System.Type[] AllArcaneSpellTypes =
    [
        typeof(ArcaneIntellect),
        typeof(Trick),
        typeof(Awaken),
        typeof(NorgannonWisdom),
        typeof(ArcaneBarrage),
        typeof(ApexisBlast),
        typeof(IgniteCard)
    ];

    /// <summary>
    /// 展开候选池：每种法术牌按其允许的升级级别生成 (类型, 升级级别) 候选
    /// （未升级 + 升级形态，升级后的法术牌同样可被检索；点燃只能未升级形态）。
    /// </summary>
    private static List<(System.Type Type, int UpgradeLevel)> ExpandPool(System.Type[] types)
    {
        var result = new List<(System.Type, int)>();
        foreach (var t in types)
        {
            result.Add((t, 0));
            var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(t));
            if (canonical == null)
            {
                continue;
            }
            int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(t);
            for (int level = 1; level <= maxLevel; level++)
            {
                result.Add((t, level));
            }
        }
        return result;
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
            // 操控时间：发现两张奥术法术牌，费用减少 1 点
            await DiscoverTwoArcane(choiceContext);
            return;
        }

        // 撕裂现实：从所有吉安娜法术牌（含升级形态）中随机将 2 张置入手牌，费用减少 1 点
        var rng = base.Owner.RunState.Rng.CombatCardSelection;
        var pool = ExpandPool(AllSpellTypes);
        for (int i = 0; i < 2; i++)
        {
            if (pool.Count == 0)
            {
                break;
            }
            var entry = rng.NextItem(pool);
            if (entry.Type == null)
            {
                break;
            }
            pool.Remove(entry);
            await GrantDiscountedCard(choiceContext, entry.Type, entry.UpgradeLevel);
        }
    }

    /// <summary>
    /// 创建一张奥术法术牌（按升级级别恢复形态），费用减少 1 点后置入手牌。
    /// </summary>
    private async Task GrantDiscountedCard(PlayerChoiceContext choiceContext, System.Type type, int upgradeLevel)
    {
        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, base.Owner, type, upgradeLevel);
        if (card == null)
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
        // 费用减少 1 点（直到打出）
        card.EnergyCost.SetUntilPlayed(card.EnergyCost.GetResolved() - 1);
        if (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(base.Owner))
        {
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, base.Owner);
        }
    }

    /// <summary>
    /// 操控时间：从吉安娜全部奥术法术牌（含升级形态）中随机取，两次三选一发现，
    /// 选中的费用减少 1 点置入手牌。
    /// </summary>
    private async Task DiscoverTwoArcane(PlayerChoiceContext choiceContext)
    {
        if (AllArcaneSpellTypes.Length == 0)
        {
            return;
        }

        var rng = base.Owner.RunState.Rng.CombatCardSelection;
        for (int i = 0; i < 2; i++)
        {
            // 随机取最多 3 个候选（不重复，含升级形态）
            var pool = ExpandPool(AllArcaneSpellTypes);
            var candidates = new List<CardModel>();
            while (candidates.Count < 3 && pool.Count > 0)
            {
                var entry = rng.NextItem(pool);
                if (entry.Type == null)
                {
                    break;
                }
                pool.Remove(entry);
                var combatState = base.Owner.Creature.CombatState;
                var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                    combatState, base.Owner, entry.Type, entry.UpgradeLevel);
                if (card != null)
                {
                    candidates.Add(card);
                }
            }
            if (candidates.Count == 0)
            {
                break;
            }
            var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, candidates.AsReadOnly(), base.Owner, canSkip: true);
            if (chosen == null)
            {
                break;
            }
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(chosen);
            // 费用减少 1 点（直到打出）
            chosen.EnergyCost.SetUntilPlayed(chosen.EnergyCost.GetResolved() - 1);
            if (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(base.Owner))
            {
                await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, base.Owner);
            }
        }
    }
}
