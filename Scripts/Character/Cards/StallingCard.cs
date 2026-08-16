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
/// 拖延时间 (Stall for Time) - 0费能力牌（稀有）。
/// 任务：施放火焰、冰霜和奥术法术各一个。奖励：发现一张火焰/冰霜/奥术派系法术牌。
/// 并升级为"抵达传送大厅"。任务线第二阶段（由巫师的计策升级而来）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class StallingCard : ModCardTemplate
{
    /// <summary>
    /// 任务（悬停解释）：任务卡专属关键词
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Quest];

    public override string CustomPortraitPath => "res://assets/card_art/stall_for_time.png";

    public StallingCard()
        : base(0, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂任务线光环：阶段 2
        var applied = await PowerCmd.Apply<MageQuestlinePower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
        if (applied is { Count: > 0 } && applied[0] is MageQuestlinePower quest)
        {
            quest.Stage = 2;
        }
    }
}
