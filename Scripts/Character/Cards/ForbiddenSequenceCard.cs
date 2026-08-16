using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 禁忌序列 (Forbidden Sequence) - 0费能力牌（稀有）。
/// 你发现8张牌后获得奖励：源生之石（打出1次禁忌序列只能获得1次奖励）。固有。
/// 升级后：你发现7张牌后获得奖励：源生之石。固有。
/// 奖励 = 源生之石直接置入手牌。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ForbiddenSequenceCard : ModCardTemplate
{
    /// <summary>
    /// 任务（悬停解释）+ 发现（悬停解释）+ 固有：战斗开始时该牌在手牌中
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Quest,
         jaina.Scripts.Character.Keywords.JainaKeywords.Discover,
         CardKeyword.Innate];

    public override string CustomPortraitPath => "res://assets/card_art/forbidden_sequence.png";

    /// <summary>
    /// 悬停提示：显示奖励衍生物"源生之石"卡。
    /// 源生之石不可升级（MaxUpgradeLevel=0），升级前后均直接显示原版——
    /// 不可对其调用 UpgradeInternal（CurrentUpgradeLevel 超出上限会抛异常导致悬停消失）。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            var canonical = ModelDb.Card<ForbiddenStoneCard>();
            yield return new CardHoverTip(canonical);
        }
    }

    public ForbiddenSequenceCard()
        : base(0, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称不变，效果阈值 8 -> 7
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            return title.GetFormattedText();
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂计数光环：每发现一张牌计数，达到阈值（升级前 8 / 升级后 7）奖励源生之石，
        // 随后光环消失（打出 1 次禁忌序列只能获得 1 次奖励）
        var applied = await PowerCmd.Apply<ForbiddenSequencePower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
        if (applied is { Count: > 0 } && applied[0] is ForbiddenSequencePower seq)
        {
            seq.Threshold = IsUpgraded ? 7 : 8;
        }
    }
}
