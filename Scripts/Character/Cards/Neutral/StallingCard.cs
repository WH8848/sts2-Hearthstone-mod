using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 拖延时间 (Stall for Time) - 0费能力牌（稀有）。
/// 任务：施放火焰、冰霜和奥术法术各一个。奖励：发现一张火焰/冰霜/奥术派系法术牌。
/// 并升级为"抵达传送大厅"。任务线第二阶段（由巫师的计策升级而来）。
/// 衍生卡：不进入吉安娜卡池，不出现在卡牌奖励与图鉴中（仅由任务奖励入手）。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class StallingCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 任务（悬停解释）：任务卡专属关键词
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Quest];

    public override string CustomPortraitPath => "res://assets/card_art/stall_for_time.png";

    /// <summary>
    /// 悬停提示：显示后续衍生卡（抵达传送大厅 → 奥术师晨拥），左侧从上到下排列。
    /// 升级版（拖延时间+）悬停显示升级衍生物（抵达传送大厅+ → 奥术师晨拥+）。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return HoverTipFactory.FromKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Discover);
            yield return new CardHoverTip(MageQuestlinePower.GetQuestlineHoverCard<ReachPortalChamberCard>(IsUpgraded));
            yield return new CardHoverTip(MageQuestlinePower.GetQuestlineHoverCard<DawngraspCard>(IsUpgraded));
        }
    }

    public StallingCard()
        : base(0, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"拖延时间+"
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new MegaCrit.Sts2.Core.Localization.LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            var upgraded = MegaCrit.Sts2.Core.Localization.LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂任务线光环：阶段 2（升级后的任务卡完成任务奖励抵达传送大厅+）
        var applied = await PowerCmd.Apply<MageQuestlinePower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
        if (applied is { Count: > 0 } && applied[0] is MageQuestlinePower quest)
        {
            quest.Stage = 2;
            quest.RewardUpgraded = IsUpgraded;
            // 打出此能力后才开始计数：打出前施放的法术不计入任务进度
            quest.StartCountingAfterPlay();
        }
    }
}
