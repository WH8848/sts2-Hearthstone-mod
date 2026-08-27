using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Cards.FreePlay;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 列王遗宝 (Royal Treasure) - 3费技能牌（罕见）。
/// 发现一张3费及以上的任意角色（全职业）卡牌，该牌本回合费用为0点。
/// 升级后：费用 3 -> 2（其余不变）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class RoyalTreasureCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌；无派系。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：炉石传说"列王遗宝"（Relic of Kings）官方原画（wiki.gg）
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/relic_of_kings.png";

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromKeyword(JainaKeywords.Discover)];

    public RoyalTreasureCard()
        : base(3, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    protected override void OnUpgrade()
    {
        // 升级：费用 3 -> 2（其余不变；升级后名称仍为"列王遗宝"）
        EnergyCost.UpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 发现一张3费及以上的任意角色（全职业）卡牌（三选一，可跳过；加入手牌）
        var chosen = await JainaDiscoverHelper.DiscoverAllClassesCardAndAddToHand(
            choiceContext, base.Owner, typeof(RoyalTreasureCard), minCost: 3);
        if (chosen != null)
        {
            // 该牌本回合费用为0（回合结束自动恢复/清除；本回合内后续打出也保持免费）
            chosen.SetToFreeForRestOfTurn();
        }
    }
}
