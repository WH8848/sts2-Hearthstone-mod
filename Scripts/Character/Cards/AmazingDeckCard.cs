using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 惊奇套牌 (Deck of Wonders) - 1费技能牌（罕见，奥术派系）。
/// 将五张惊奇卡牌洗入你的抽牌堆。抽到时随机施放一个全角色卡牌。
/// 升级后变为旅社谍战 (Agency Espionage)：将每个其他角色的各一张牌洗入你的抽牌堆，
/// 其法力值消耗为0点。抽取其中一张。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class AmazingDeckCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 可升级（升级后变为旅社谍战）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 关键词：基础版 法术 + 奥术；升级版（旅社谍战）法术（无派系）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [JainaKeywords.Spell]
        : [JainaKeywords.Spell, JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：惊奇套牌 / 升级后（旅社谍战）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/agency_espionage.png" : "res://assets/card_art/deck_of_wonders.png";

    public AmazingDeckCard()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 悬停提示：左侧显示衍生的惊奇卡牌（参考灵体采集者显示小精灵）。
    /// 升级后（旅社谍战）不再产生惊奇卡牌，不显示衍生卡悬停。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            if (!IsUpgraded)
            {
                yield return new CardHoverTip(ModelDb.Card<AmazingCard>());
            }
        }
    }

    /// <summary>
    /// 升级后卡牌名称变为"旅社谍战"
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

        var owner = base.Owner;
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        if (IsUpgraded)
        {
            // 旅社谍战：将每个其他角色的各一张牌洗入抽牌堆（随机位置），费用变为 0，抽取其中一张
            var rng = owner.RunState.Rng.CombatTargets;
            var jainaPool = ModelDb.CardPool<jaina.Scripts.Character.JainaCardPool>();
            var shuffled = new List<CardModel>();
            foreach (var pool in ModelDb.AllCharacterCardPools)
            {
                if (pool == jainaPool)
                {
                    continue; // 排除自己（吉安娜）
                }
                var candidates = pool.AllCards.Where(c => c != null).ToList();
                if (candidates.Count == 0)
                {
                    continue;
                }
                var chosen = rng.NextItem(candidates);
                if (chosen == null)
                {
                    continue;
                }
                var copy = combatState.CreateCard(chosen, owner);
                if (copy == null)
                {
                    continue;
                }
                copy.EnergyCost.SetCustomBaseCost(0);
                jaina.Scripts.Character.JainaCastTracker.MarkGenerated(copy);
                shuffled.Add(copy);
            }

            var results = await CardPileCmd.AddGeneratedCardsToCombat(shuffled, PileType.Draw, owner, CardPilePosition.Random);
            // 原版洗入动画（牌面预览展示，速度与原版一致）+ 刷新抽牌堆计数
            CardCmd.PreviewCardPileAdd(results);
            RefreshDrawPileCount(owner, results);

            // 抽取其中一张（洗入抽牌堆的其它角色的卡牌之一入手）
            var shuffledCards = results.Where(r => r.success).Select(r => r.cardAdded).ToList();
            if (shuffledCards.Count > 0)
            {
                var picked = rng.NextItem(shuffledCards);
                if (picked != null)
                {
                    await CardPileCmd.Add(picked, PileType.Hand);
                }
            }
            return;
        }

        // 惊奇套牌：将五张惊奇卡牌洗入抽牌堆（随机位置）
        var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(typeof(AmazingCard)));
        if (canonical == null)
        {
            return;
        }
        var cards = new List<CardModel>();
        for (int i = 0; i < 5; i++)
        {
            var card = combatState.CreateCard(canonical, owner);
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
            cards.Add(card);
        }
        var addResults = await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Draw, owner, CardPilePosition.Random);
        // 原版洗入动画（牌面预览展示，速度与原版一致）+ 刷新抽牌堆计数
        CardCmd.PreviewCardPileAdd(addResults);
        RefreshDrawPileCount(owner, addResults);
    }

    /// <summary>
    /// 洗入后刷新抽牌堆计数：新生成的洗入牌没有 NCard 节点，原版 tween 流程
    /// 不会为它们触发 CardAddFinished（→ 抽牌堆数字不刷新），这里手动触发，
    /// 与原版洗入预览动画并行（计数 +1 bump 动画与原版一致）。
    /// </summary>
    private static void RefreshDrawPileCount(Player owner, IReadOnlyList<CardPileAddResult> results)
    {
        if (results.Count == 0)
        {
            return;
        }
        try
        {
            foreach (var r in results)
            {
                if (r.success)
                {
                    r.cardAdded.Pile?.InvokeCardAddFinished();
                }
            }
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] refresh draw count failed: {ex}");
        }
    }
}
