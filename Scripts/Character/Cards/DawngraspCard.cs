using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 奥术师晨拥 (Arcanist Dawngrasp) - 吉安娜随从卡。召唤 8/8 的 DawngraspMinion。
/// 战吼：力量+2（升级版"奥术师晨拥+"为力量+3）。由"抵达传送大厅"任务奖励入手（稀有）。
/// 衍生卡：不进入吉安娜卡池，不出现在卡牌奖励与图鉴中（仅由任务奖励入手）。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class DawngraspCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 战吼（悬停解释）；基础版消耗，升级后去除消耗词条（不再消耗）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry, CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/arcanist_dawngrasp.png";
    protected override Type MinionType => typeof(DawngraspMinion);

    /// <summary>
    /// 奥术师晨拥有升级形态（晨拥+ 战吼力量+3），保持可升级
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    protected override int MinionAttack => 8;

    protected override int MinionHealth => 8;

    public DawngraspCard()
        : base(2, CardRarity.Rare)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"奥术师晨拥+"
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new MegaCrit.Sts2.Core.Localization.LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            var upgraded = MegaCrit.Sts2.Core.Localization.LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    /// <summary>
    /// 升级：去除消耗词条（LocalKeywords 懒初始化只算一次，
    /// 升级形态 Keywords 缓存自基础状态——需显式移除 Exhaust）
    /// </summary>
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
