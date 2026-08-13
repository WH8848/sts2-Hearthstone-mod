using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 奥术智慧 (Arcane Intellect) - 吉安娜专属技能牌。
/// 1费：抽两张牌。
/// 升级后变为"时空提速 (Chrono Boost)"：抽两张牌，并召唤一个 3/4 狂热者。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ArcaneIntellect : ModCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    /// <summary>
    /// 卡牌原画：炉石传说"奥术智慧"官方高清原画。
    /// 升级后变为"时空提速 (Chrono Boost)"，卡图同步切换为 Chrono Boost 官方原画。
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded
            ? "res://assets/card_art/chrono_boost.png"
            : "res://assets/card_art/arcane_intellect.png";

    public ArcaneIntellect()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"时空提速 (Chrono Boost)"
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

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, base.Owner);

        // 升级后：召唤狂热者（3/4，生成时立刻攻击）
        if (IsUpgraded)
        {
            await JainaMinionPool.SummonMinion<Zealot>(choiceContext, base.Owner, maxHp: 4m, attack: 3m);
        }
    }

    protected override void OnUpgrade()
    {
        // 升级后名称为"时空提速"，效果为抽牌+召唤狂热者
        // 卡牌 ID 不变，通过 titleUpgraded 本地化键显示新名称
    }
}