using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 冰血哨塔 (Iceblood Tower) - 3费能力牌（罕见）。
/// 在你的回合结束时，从你的抽牌堆中抽一张法术牌并打出（抽牌堆没有法术时从弃牌堆抽）。
/// 可叠层：每张冰血哨塔在回合结束各触发一次。打出的法术不被消耗（进弃牌堆）。
/// 升级后费用变为 2。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class IcebloodTowerCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 可升级（升级后费用 3 -> 2）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 法术牌关键词：冰血哨塔视为法术牌（可被咒术洪流减费、可被复制等）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [jaina.Scripts.Character.Keywords.JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/iceblood_tower.png";

    public IcebloodTowerCard()
        : base(3, CardType.Power, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级：费用 3 -> 2
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂冰血哨塔（可叠层：每张哨塔在回合结束各触发一次，
        // 从抽牌堆抽一张法术打出，抽牌堆没有则从弃牌堆抽；打出的法术不被消耗）
        await PowerCmd.Apply<IcebloodTowerPower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
