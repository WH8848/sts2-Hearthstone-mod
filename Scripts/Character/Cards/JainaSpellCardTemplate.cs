using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜法术/技能牌基类（继承 ModCardTemplate）。
/// 关键词悬停解释由游戏原版 CardModel.HoverTips 对卡上 Keywords 自动生成
/// （RitsuLib HoverTipFactoryFromKeywordPatch 提供注册的提示），无需手动遍历。
/// 子类按需覆写 AdditionalHoverTips 追加非关键词提示（如衍生物卡/无实体解释）。
/// </summary>
public abstract class JainaSpellCardTemplate : ModCardTemplate
{
    protected JainaSpellCardTemplate(int baseCost, CardType type, CardRarity rarity, TargetType target, bool showInCardLibrary = true)
        : base(baseCost, type, rarity, target, showInCardLibrary)
    {
    }
}
