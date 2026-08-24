using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 奥术智慧 (Arcane Intellect) - 吉安娜专属技能牌。
/// 1费：抽两张牌。
/// 升级后变为"清凉的泉水 (Cooling Spring)"：抽 2 张牌，每抽到一张法术牌复原 1 能量
/// （复原：只补到能量上限，不超出上限——与"获得"不同，不会产生超额能量）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ArcaneIntellect : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌；基础版奥术派系，
    /// 升级后（清凉的泉水）无派系。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    /// <summary>
    /// 卡牌原画：奥术智慧官方原画 / 升级后（清凉的泉水）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded
            ? "res://assets/card_art/refreshing_spring_water.png"
            : "res://assets/card_art/arcane_intellect.png";

    public ArcaneIntellect()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"清凉的泉水 (Cooling Spring)"
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

        var drawn = await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, base.Owner);

        // 清凉的泉水（升级后）：每抽到一张法术牌（攻击/技能）复原 1 能量——
        // 复原 = 只补到能量上限（不超出上限；能量已满则不再增加）
        if (IsUpgraded)
        {
            var pcs = base.Owner.PlayerCombatState;
            foreach (var card in drawn)
            {
                if (card.Type == CardType.Attack || card.Type == CardType.Skill)
                {
                    if (pcs != null && pcs.Energy < pcs.MaxEnergy)
                    {
                        await PlayerCmd.GainEnergy(Math.Min(1m, pcs.MaxEnergy - pcs.Energy), base.Owner);
                    }
                }
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 升级为清凉的泉水：移除奥术派系（升级形态无派系）。
        // LocalKeywords 懒缓存可能已在未升级状态初始化——需显式移除。
        RemoveKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Arcane);
    }
}
