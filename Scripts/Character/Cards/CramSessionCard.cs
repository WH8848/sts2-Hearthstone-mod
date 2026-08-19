using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 考前刷夜 (Cram Session) - 0费技能牌（罕见，奥术派系）。
/// 抽1张牌，每有1点力量多抽1张。
/// 升级后变为"观星 (Stargazing)"：抽一张不同的奥术法术牌。
/// 这张奥术法术牌本回合内具有重放1（打出后自动重放一次）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class CramSessionCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 奥术派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：考前刷夜 / 升级后（观星）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/stargazing.png" : "res://assets/card_art/cram_session.png";

    public CramSessionCard()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"观星 (Stargazing)"
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
            // 观星：抽一张不同的奥术法术牌（奥术派系攻击/技能牌，非本卡），
            // 该牌本回合内具有重放1（打出后自动重放一次）
            await PlayAsStargazing(choiceContext);
            return;
        }

        // 考前刷夜：抽 1 张牌，每有 1 点力量多抽 1 张（力量 0 时抽 1 张，最低 1 张）
        int strength = base.Owner.Creature.GetPowerAmount<StrengthPower>();
        int drawCount = Math.Max(1, 1 + strength);
        await CardPileCmd.Draw(choiceContext, drawCount, base.Owner);
    }

    /// <summary>
    /// 观星效果：从抽牌堆中找一张"不同的"奥术法术牌（非本卡）置入手牌，
    /// 并给该牌挂"本回合重放1"（StargazingReplayPower：该牌打出时施放两次）。
    /// 抽牌堆没有奥术法术牌时，从弃牌堆找一张奥术派系的法术牌；
    /// 两处都没有则不生效（不抽牌）。
    /// </summary>
    private async Task PlayAsStargazing(PlayerChoiceContext choiceContext)
    {
        var arcaneSpell = FindArcaneSpell(PileType.Draw)
            ?? FindArcaneSpell(PileType.Discard);
        if (arcaneSpell == null)
        {
            // 抽牌堆与弃牌堆都没有奥术法术牌：不生效
            return;
        }
        // 手牌满时 Add 自动改道弃牌堆（原版满手语义，牌不消失不消耗）
        await CardPileCmd.Add(arcaneSpell, PileType.Hand);

        // 挂重放1：该牌本回合内打出时自动重放一次（用一次即消耗，回合结束也移除）
        var applied = await PowerCmd.Apply<jaina.Scripts.Character.Powers.StargazingReplayPower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
        if (applied is { Count: > 0 } && applied[0] is jaina.Scripts.Character.Powers.StargazingReplayPower replay)
        {
            replay.Target = arcaneSpell;
        }
    }

    /// <summary>
    /// 在指定牌堆中找第一张"不同的"奥术法术牌（奥术派系攻击/技能牌，非本卡）
    /// </summary>
    private CardModel? FindArcaneSpell(PileType pileType)
    {
        var pile = pileType.GetPile(base.Owner);
        return pile?.Cards.FirstOrDefault(c =>
            c != null &&
            c.GetType() != typeof(CramSessionCard) &&
            (c.Type == CardType.Attack || c.Type == CardType.Skill) &&
            c.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Arcane));
    }
}
