using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 星术师索兰莉安 (Astromancer Solarian) - 0费随从卡（稀有）。
/// 力量+1。亡语：将"终极索兰莉安"洗入你的牌库。属性 3/2。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class AstromancerSolarianCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"星术师索兰莉安"（Astromancer Solarian, BT_028）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/astromancer_solarian.png";

    protected override Type MinionType => typeof(AstromancerSolarianMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 2;

    /// <summary>
    /// 亡语（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Deathrattle, CardKeyword.Exhaust];

    public AstromancerSolarianCard()
        : base(0, CardRarity.Rare)
    {
    }

    /// <summary>
    /// 自身特性悬停：亡语洗入牌库的衍生物"终极索兰莉安"卡（"随从"关键词解释由模板兜底）
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips
    {
        get
        {
            yield return new CardHoverTip(ModelDb.Card<SolarianPrimeCard>());
        }
    }
}
