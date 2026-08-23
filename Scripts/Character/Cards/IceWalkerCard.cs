using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;
using MegaCrit.Sts2.Core.HoverTips;
using jaina.Scripts.Character.Powers;
using jaina.Scripts.Character.Keywords;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 寒冰行者 (Ice Walker) - 0费随从卡（普通）。
/// 你的英雄技能还会给与目标1层冻结。属性 1/3。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class IceWalkerCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"寒冰行者"（Ice Walker, ICC_212）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/ice_walker.png";

    protected override Type MinionType => typeof(IceWalkerMinion);

    protected override int MinionAttack => 1;

    protected override int MinionHealth => 3;

    /// <summary>
    /// 自身特性悬停：冻结能力解释（"随从"关键词解释由模板兜底）
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips => [HoverTipFactory.FromPower<FreezePower>()];

    /// <summary>
    /// 元素种族 + 冻结（悬停解释,英雄技能额外给予冻结）+ 消耗（随从卡打出后消耗,模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Elemental,
         jaina.Scripts.Character.Keywords.JainaKeywords.Freeze,
         CardKeyword.Exhaust];

    public IceWalkerCard()
        : base(0, CardRarity.Common)
    {
    }
}
