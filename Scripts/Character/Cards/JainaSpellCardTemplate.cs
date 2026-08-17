using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜法术/技能牌基类（继承 ModCardTemplate）。
/// 关键词悬停解释由游戏原版 CardModel.HoverTips 对卡上 Keywords 自动生成
/// （RitsuLib HoverTipFactoryFromKeywordPatch 提供注册的提示），无需手动遍历。
/// 子类按需覆写 AdditionalHoverTips 追加非关键词提示（如衍生物卡/无实体解释）。
/// 手牌发光（仅对局内衍生卡显示浓天蓝）由 JainaHandGlowPatch 统一处理。
/// </summary>
public abstract class JainaSpellCardTemplate : ModCardTemplate
{
    protected JainaSpellCardTemplate(int baseCost, CardType type, CardRarity rarity, TargetType target, bool showInCardLibrary = true)
        : base(baseCost, type, rarity, target, showInCardLibrary)
    {
    }

    /// <summary>
    /// 能力牌（Power 类型）打出后的去向与杀戮尖塔2原版机制一致：
    /// 强制返回 PileType.None（打出后从对局中移除，不进入弃牌堆，也不显示"消耗"标签）。
    /// 原版 CardModel.GetResultLocationForCardPlay 对 Power 类型同样返回 None，
    /// 这里显式保证 mod 能力卡不受任何扩展逻辑影响。
    /// </summary>
    protected override CardLocation GetResultLocationForCardPlay()
    {
        if (Type == CardType.Power)
        {
            return new CardLocation(Owner, PileType.None, CardPilePosition.Bottom);
        }
        return base.GetResultLocationForCardPlay();
    }
}
