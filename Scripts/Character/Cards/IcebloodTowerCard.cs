using System.Collections.Generic;
using System.Linq;
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
/// 在你的回合结束时，随机从你的抽牌堆中施放另一个法术。
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

        // 顶替旧的冰血哨塔效果（打出新哨塔替换旧效果）
        var old = base.Owner.Creature.Powers.OfType<IcebloodTowerPower>().FirstOrDefault();
        if (old != null)
        {
            await PowerCmd.Remove(old);
        }

        // 挂冰血哨塔（回合结束时随机施放抽牌堆法术）
        await PowerCmd.Apply<IcebloodTowerPower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
