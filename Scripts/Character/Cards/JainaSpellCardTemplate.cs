using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜法术/技能牌基类（继承 ModCardTemplate）。
/// 统一提供关键词悬停提示：卡面右侧显示各关键词（法术牌/派系/压轴等）的详细解释。
/// </summary>
public abstract class JainaSpellCardTemplate : ModCardTemplate
{
    protected JainaSpellCardTemplate(int baseCost, CardType type, CardRarity rarity, TargetType target, bool showInCardLibrary = true)
        : base(baseCost, type, rarity, target, showInCardLibrary)
    {
    }

    /// <summary>
    /// 悬停提示：卡面右侧显示各关键词（法术牌/火焰/冰霜/奥术/压轴/英雄技能等）的详细解释。
    /// 通过 RitsuLib 补丁后的 HoverTipFactory.FromKeyword 支持自定义关键词。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            foreach (var keyword in CanonicalKeywords)
            {
                yield return HoverTipFactory.FromKeyword(keyword);
            }
        }
    }
}
