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
/// 冰血哨塔 (Iceblood Tower) - 吉安娜专属先古能力牌（2 费，冰霜派系）。
/// 在你的回合结束时，随机从你的抽牌堆中施放另一个法术。持续 3 回合。
/// 升级后费用 2 -> 1。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterDustyTomeCard(typeof(jaina.Scripts.Character.Jaina))]
public sealed class IcebloodTowerCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 冰霜派系（能力牌，不挂"法术牌"关键词，避免进入法术池/减费/复制）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Frost];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/iceblood_tower.png";

    public IcebloodTowerCard()
        : base(2, CardType.Power, CardRarity.Ancient, TargetType.None, true)
    {
    }

    protected override void OnUpgrade()
    {
        // 升级：费用 2 -> 1
        EnergyCost.UpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂冰血哨塔：接下来 3 个回合结束时各随机施放一张抽牌堆法术
        await PowerCmd.Apply<IcebloodTowerPower>(
            choiceContext, [base.Owner.Creature], 3m, base.Owner.Creature, this);
    }
}
