using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 惊奇卡牌 (Scroll of Wonder) - 0费状态牌（衍生）。
/// 抽到时施放随机施放一个全角色卡牌，释放后此卡消耗。
/// 由惊奇套牌洗入抽牌堆；不进入掉落池。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class AmazingCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 消耗：释放后此卡消耗
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/scroll_of_wonder.png";

    public AmazingCard()
        : base(0, CardType.Status, CardRarity.Token, TargetType.None, false)
    {
    }
}
