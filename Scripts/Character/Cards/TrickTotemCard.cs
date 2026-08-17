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
/// 戏法图腾 (Trick Totem) - 1费能力牌（罕见）。
/// 在你的回合结束时，随机施放一个费用消耗小于或等于1点的全角色卡牌。
/// 升级后费用变为 0。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class TrickTotemCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 可升级（升级后费用 1 -> 0）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/trick_totem.png";

    public TrickTotemCard()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级：费用 1 -> 0
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂戏法图腾（可叠层：每张 +1 层，回合结束每层各施放一次）
        await PowerCmd.Apply<TrickTotemPower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
