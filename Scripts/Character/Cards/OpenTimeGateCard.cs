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
/// 打开时空之门 (Open the Time Gate) - 0费能力牌（稀有，固有）。
/// 施放8个你的牌库之外的法术牌后获得奖励：时空扭曲（打出1次只能获得1次奖励）。
/// 升级后（打开时空之门+）：奖励时空扭曲+。
/// 奖励 = 时空扭曲直接置入手牌。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class OpenTimeGateCard : ModCardTemplate
{
    /// <summary>
    /// 固有：战斗开始时该牌在手牌中（游戏原生关键词）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];

    public override string CustomPortraitPath => "res://assets/card_art/open_time_gate.png";

    /// <summary>
    /// 悬停提示：显示奖励衍生物"时空扭曲"卡（参考冰冷案例/时空提速）
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return new CardHoverTip(ModelDb.Card<TimeWarpCard>());
        }
    }

    public OpenTimeGateCard()
        : base(0, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"打开时空之门+"
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂计数光环：施放 8 个牌库之外的法术牌后奖励时空扭曲（升级后为时空扭曲+），
        // 随后光环消失（打出 1 次打开时空之门只能获得 1 次奖励）
        var applied = await PowerCmd.Apply<OpenTimeGatePower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
        if (applied is { Count: > 0 } && applied[0] is OpenTimeGatePower gate)
        {
            gate.RewardUpgraded = IsUpgraded;
        }
    }
}
