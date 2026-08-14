using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 远古雕文 (Ancient Glyph) - 0费：发现一张法术牌，使其费用减少 1 点。
/// 升级后变为巅峰无限 (Peak Infinity)：发现一张法术牌，使其费用减少 1 点。
/// 压轴：在回合结束时将本牌移回你的手牌。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class Awaken : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌关键词 + 升级后压轴关键词（卡面显示压轴词条）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded
            ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Finisher, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane]
            : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/peak_infinity.png" : "res://assets/card_art/awaken.png";

    public Awaken()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.None, true)
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

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/诺干农派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 远古雕文/巅峰无限：发现一张法术牌，使其费用减少 1 点
        var chosen = await JainaDiscoverHelper.DiscoverAndAddToHand(choiceContext, base.Owner, maxCost: 9);
        if (chosen != null && chosen.EnergyCost.Canonical > 0)
        {
            // 直接减少 1 点展示费用（mutable 实例）
            chosen.EnergyCost.SetUntilPlayed((int)chosen.EnergyCost.Canonical - 1);
        }

        // 巅峰无限（升级后）压轴：刚好消耗完能量时，回合结束将本牌移回手牌
        var energy = base.Owner.PlayerCombatState?.Energy;
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] Awaken OnPlay: upgraded={IsUpgraded} energy={energy}");
        if (IsUpgraded && energy is <= 0)
        {
            var power = await PowerCmd.Apply<jaina.Scripts.Character.Powers.PeakInfinityPower>(
                choiceContext, base.Owner.Creature, 1m, base.Owner.Creature, this);
            MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDebug] Awaken PeakInfinityPower applied: {(power != null)}");
            if (power != null)
            {
                power.TargetCard = this;
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 升级为巅峰无限：加入压轴关键词（LocalKeywords 懒缓存可能已在未升级状态初始化）
        AddKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Finisher);
    }
}
