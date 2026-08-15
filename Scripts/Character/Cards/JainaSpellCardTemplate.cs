using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MinionLib.Utilities.CustomGlowColor;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜法术/技能牌基类（继承 ModCardTemplate）。
/// 关键词悬停解释由游戏原版 CardModel.HoverTips 对卡上 Keywords 自动生成
/// （RitsuLib HoverTipFactoryFromKeywordPatch 提供注册的提示），无需手动遍历。
/// 子类按需覆写 AdditionalHoverTips 追加非关键词提示（如衍生物卡/无实体解释）。
/// 实现 MinionLib 自定义发光：手牌中的法术牌显示蓝色发光标记（仅手牌，牌库查看不显示）。
/// </summary>
public abstract class JainaSpellCardTemplate : ModCardTemplate, ICustomGlowColorCard
{
    protected JainaSpellCardTemplate(int baseCost, CardType type, CardRarity rarity, TargetType target, bool showInCardLibrary = true)
        : base(baseCost, type, rarity, target, showInCardLibrary)
    {
    }

    /// <summary>
    /// 手牌发光颜色：法术蓝（英雄技能等子类可覆写为 null 取消发光）
    /// </summary>
    public virtual Color? GlowColor => new Color(0.35f, 0.65f, 1f);
}
