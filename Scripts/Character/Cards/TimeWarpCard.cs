using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 时空扭曲 (Time Warp) - 2费技能牌（衍生卡，稀有，奥术派系）。
/// 获得一个额外回合。（每局对战限一次——升级后解除限制）
/// 由打开时空之门作为奖励直接置入手牌。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class TimeWarpCard : ModCardTemplate
{
    /// <summary>
    /// 奥术派系（悬停解释）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    public override string CustomPortraitPath => "res://assets/card_art/time_warp.png";

    public TimeWarpCard()
        : base(2, CardType.Skill, CardRarity.Token, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"时空扭曲+"，解除"每局对战限一次"限制
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

    /// <summary>
    /// 未升级：每局对战限一次（本局已使用过时空扭曲后不可再打出）
    /// </summary>
    protected override bool IsPlayable
    {
        get
        {
            if (base.Owner?.Creature?.CombatState == null || IsUpgraded)
            {
                return true;
            }
            return !jaina.Scripts.Character.JainaCastTracker.IsTimeWarpUsedThisCombat(base.Owner.Creature.CombatState);
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 每局对战限一次（未升级）：记录本局已使用
        if (!IsUpgraded && base.Owner?.Creature?.CombatState != null)
        {
            jaina.Scripts.Character.JainaCastTracker.MarkTimeWarpUsed(base.Owner.Creature.CombatState);
        }

        // 挂一次性额外回合光环（结束当前回合后获得一个额外回合）
        await PowerCmd.Apply<TimeWarpPower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        // 升级为时空扭曲+：解除"每局对战限一次"限制（费用保持 2）
    }
}
