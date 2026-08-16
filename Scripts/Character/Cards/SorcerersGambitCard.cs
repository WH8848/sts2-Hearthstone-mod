using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 巫师的计策 (Sorcerer's Gambit) - 0费能力牌（稀有）。
/// 任务：施放火焰、冰霜和奥术法术各一个。奖励：抽一张法术牌。并升级为"拖延时间"。
/// 任务线第一阶段。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class SorcerersGambitCard : ModCardTemplate
{
    /// <summary>
    /// 任务（悬停解释）+ 固有：战斗开始时该牌在手牌中
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Quest, CardKeyword.Innate];

    public override string CustomPortraitPath => "res://assets/card_art/sorcerers_gambit.png";

    public SorcerersGambitCard()
        : base(0, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂任务线光环：阶段 1
        await PowerCmd.Apply<MageQuestlinePower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
