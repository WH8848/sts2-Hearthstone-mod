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
/// 云雾王子 (Cloud Prince) - 2费随从卡（普通，元素）。
/// 战吼：选择1名敌人，你的状态栏中每有1种状态，则对其造成6点伤害。属性 4/4。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class CloudPrinceCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"云雾王子"（Cloud Prince, SoU_54493）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/cloud_prince.png";

    /// <summary>
    /// 元素种族 + 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Elemental, JainaKeywords.Battlecry, CardKeyword.Exhaust];

    /// <summary>
    /// 战吼选择 1 名敌人（打出时进入目标选择）
    /// </summary>
    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override Type MinionType => typeof(CloudPrinceMinion);

    protected override int MinionAttack => 4;

    protected override int MinionHealth => 4;

    public CloudPrinceCard()
        : base(2, CardRarity.Common)
    {
    }

    /// <summary>
    /// 打出：把战吼选择的目标传给随从（召唤时随从触发战吼读取；
    /// 静态传递，召唤完成后清除）。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CloudPrinceMinion.BattlecryTargetOverride = cardPlay.Target;
        try
        {
            await base.OnPlay(choiceContext, cardPlay);
        }
        finally
        {
            CloudPrinceMinion.BattlecryTargetOverride = null;
        }
    }
}
