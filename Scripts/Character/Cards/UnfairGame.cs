using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 加工失误 (Manufacturing Error) - 1费技能牌（罕见）。
/// 抽三张牌。如果你的抽牌堆里没有随从牌，这三张牌的费用消耗减少1点。
/// 升级后变为加大音量 (Turn Up Volume)：抽三张法术牌。
/// 压轴：如果刚好消耗完能量，从抽到的三张法术牌中发现一张复制。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class UnfairGame : JainaSpellCardTemplate, Powers.IJainaConditionGlowCard
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌。
    /// 升级后（加大音量）：奥术派系 + 压轴关键词。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Finisher, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/volume_up.png" : "res://assets/card_art/manufacturing_error.png";

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Discover)];

    public UnfairGame()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

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

    protected override void OnUpgrade()
    {
        // 升级为加大音量：奥术派系 + 压轴关键词。
        // 需显式加入：LocalKeywords 懒初始化只算一次，升级前缓存的 Keywords
        // 不含 Arcane/Finisher，悬停提示（原版 HoverTips 遍历 Keywords）不会出现奥术/压轴解释。
        AddKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Finisher);
        AddKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Arcane);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        if (IsUpgraded)
        {
            // 加大音量：抽三张法术牌（攻击/技能牌，或带"法术牌"关键词的能力牌）。
            // 从抽牌堆中逐张挑法术牌入手（跳过随从/诅咒/状态等非法术牌）；
            // 抽牌堆不足 3 张 → 从弃牌堆补足（统一语义见 JainaDrawHelper）。
            // 注意：取牌堆中的卡入手必须用 CardPileCmd.Add（满手时原版语义改道弃牌堆，
            // 与圣殿蜡烛商一致）；不能走 GrantOrQueue/AddGeneratedCardToCombat——
            // 卡已有牌堆，引擎禁止"生成已有牌堆的卡"（会抛异常卡住打出流程）。
            var drawn = new List<CardModel>();
            var spellCandidates = jaina.Scripts.Character.JainaDrawHelper.PickMatchingFromDrawThenDiscard(
                base.Owner, 3,
                c => jaina.Scripts.Character.JainaCastTracker.IsSpellCard(c));
            foreach (var spell in spellCandidates)
            {
                drawn.Add(spell);
                await CardPileCmd.Add(spell, PileType.Hand);
            }
            // 压轴：如果刚好消耗完能量，从抽到的三张法术牌中发现一张复制
            if (base.Owner.PlayerCombatState is { Energy: <= 0 })
            {
                var spells = drawn.Where(c => c.Type == CardType.Attack || c.Type == CardType.Skill).ToList();
                if (spells.Count > 0)
                {
                    // 发现界面要求最多 3 张候选；抽到的法术牌超过 3 张时随机取 3 张
                    var candidates = spells.Select(c => c.CreateClone()).ToList();
                    if (candidates.Count > 3)
                    {
                        var rng = base.Owner.RunState.Rng.CombatTargets;
                        candidates = candidates.OrderBy(_ => rng.NextInt(1 << 30)).Take(3).ToList();
                    }
                    var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, candidates.AsReadOnly(), base.Owner, canSkip: true);
                    if (chosen != null)
                    {
                        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(chosen);
                        if (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(base.Owner))
                        {
                            await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, base.Owner);
                        }
                    }
                }
            }
        }
        else
        {
            // 加工失误：抽三张牌；抽牌堆无随从牌则这三张牌费用减 1
            var hasMinionInDrawPile = base.Owner.PlayerCombatState?.DrawPile.Cards.Any(
                c => c.Type == JainaCardTypes.Minion) ?? false;
            var drawn = await CardPileCmd.Draw(choiceContext, 3, base.Owner);
            if (!hasMinionInDrawPile)
            {
                foreach (var card in drawn)
                {
                    // 减费直到打出（SetUntilPlayed：只在打出前显示减费，打出后恢复）
                    card.EnergyCost.SetUntilPlayed(card.EnergyCost.GetResolved() - 1);
                }
            }
        }
    }
}
