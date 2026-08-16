using System;
using System.Collections.Generic;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 小精灵驾驭者 (Imp Wrangler) - 2费随从卡（普通）。
/// 战吼：灌注并触发你的英雄技能。属性 4/4。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ImpWranglerCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：程序绘制的"小精灵驾驭者"占位图（无炉石原卡）
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/imp_wrangler.png";

    protected override Type MinionType => typeof(ImpWranglerMinion);

    protected override int MinionAttack => 4;

    protected override int MinionHealth => 4;

    /// <summary>
    /// 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry,
         jaina.Scripts.Character.Keywords.JainaKeywords.Empower,
         CardKeyword.Exhaust];

    public ImpWranglerCard()
        : base(2, CardRarity.Common)
    {
    }
}
