using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 潮汐之池的"施放法术后重新开启"追踪（挂在<b>地标实体</b>上）。
/// 你（地标主人）每施放一张法术牌（攻击/技能牌或挂"法术牌"关键词的卡，不含英雄技能），
/// 重新开启本地标：移除冷却并立即重新授予行动点——当回合即可再次点击使用。
/// </summary>
[RegisterPower]
public sealed class TidePoolTrackerPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 不可见（纯逻辑追踪，不显示图标）
    /// </summary>
    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 玩家打出卡后：若为地标主人施放的法术牌，重新开启地标
    /// （移除冷却并立即重新授予使用行动点——当回合即可再次点击使用）。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner;
        if (owner == null)
        {
            return;
        }
        var card = cardPlay.Card;
        if (card == null || card.Owner != owner.PetOwner)
        {
            return;
        }
        // 英雄技能（火焰冲击等）不是法术牌，不触发
        if (card.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.HeroPower))
        {
            return;
        }
        // 法术牌 = 统一判定（攻击/技能，或带"法术牌"关键词的能力牌；随从/地标不算）
        bool isSpell = jaina.Scripts.Character.JainaCastTracker.IsSpellCard(card);
        if (!isSpell)
        {
            return;
        }
        if (owner.Monster is Minions.JainaLandmarkBase landmark)
        {
            await landmark.Reactivate(choiceContext);
        }
    }
}
