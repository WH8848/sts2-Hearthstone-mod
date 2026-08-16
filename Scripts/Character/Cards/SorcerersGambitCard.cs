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
/// 巫师的计策 (Sorcerer's Gambit) - 0费能力牌（稀有）。
/// 任务：施放火焰、冰霜和奥术法术各一个。奖励：抽一张法术牌。并升级为"拖延时间"。
/// 任务线第一阶段。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class SorcerersGambitCard : ModCardTemplate
{
    /// <summary>
    /// 法术牌 + 任务（悬停解释）+ 固有：战斗开始时该牌在手牌中。
    /// 视为法术牌（可被复制），但任务线卡不可被发现（不在任何发现池中）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell,
         jaina.Scripts.Character.Keywords.JainaKeywords.Quest,
         CardKeyword.Innate];

    public override string CustomPortraitPath => "res://assets/card_art/sorcerers_gambit.png";

    /// <summary>
    /// 悬停提示：显示整条任务线的后续衍生卡（拖延时间 → 抵达传送大厅 → 奥术师晨拥），
    /// 左侧从上到下排列（NHoverTipCardContainer 按添加顺序垂直布局）。
    /// 升级版（巫师的计策+）悬停显示升级衍生物（拖延时间+ → 抵达传送大厅+ → 奥术师晨拥+）。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return new CardHoverTip(MageQuestlinePower.GetQuestlineHoverCard<StallingCard>(IsUpgraded));
            yield return new CardHoverTip(MageQuestlinePower.GetQuestlineHoverCard<ReachPortalChamberCard>(IsUpgraded));
            yield return new CardHoverTip(MageQuestlinePower.GetQuestlineHoverCard<DawngraspCard>(IsUpgraded));
        }
    }

    public SorcerersGambitCard()
        : base(0, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"巫师的计策+"
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

        // 挂任务线光环：阶段 1（升级后的任务卡完成任务奖励拖延时间+）
        var applied = await PowerCmd.Apply<MageQuestlinePower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
        if (applied is { Count: > 0 } && applied[0] is MageQuestlinePower quest)
        {
            quest.RewardUpgraded = IsUpgraded;
        }
    }
}
