using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 滑冰元素 (Sleet Skater) - 1费随从卡（罕见）。
/// 微缩，战吼：选择1名敌人，给予其1层冻结，获得等同于其减少的总体伤害的格挡。属性 3/4。
/// 微型复制品（0费1/1）由微缩系统自动生成，保留全部文字效果。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class SkatingElementalCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"滑冰元素"（Sleet Skater, WBW_103348）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/sleet_skater.png";

    /// <summary>
    /// 微缩（打出后生成0费1/1微型复制品）+ 战吼（悬停解释）+ 消耗（随从卡打出后消耗,模板默认;
    /// 微型复制品生成时自动去除消耗）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Miniaturize, JainaKeywords.Battlecry, CardKeyword.Exhaust];

    /// <summary>
    /// 战吼选择 1 名敌人（打出时进入目标选择）
    /// </summary>
    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override Type MinionType => typeof(SkatingElementalMinion);

    protected override int MinionAttack => 3;

    protected override int MinionHealth => 4;

    public SkatingElementalCard()
        : base(1, CardRarity.Uncommon)
    {
    }

    /// <summary>
    /// 打出：把战吼选择的目标传给随从（召唤时随从触发战吼读取；
    /// 静态传递，召唤完成后清除）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        SkatingElementalMinion.BattlecryTargetOverride = cardPlay.Target;
        try
        {
            await base.OnPlay(choiceContext, cardPlay);
        }
        finally
        {
            SkatingElementalMinion.BattlecryTargetOverride = null;
        }
    }
}
