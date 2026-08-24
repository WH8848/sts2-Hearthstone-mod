using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 小玩物小屋的"抽到的牌"追踪（挂在<b>地标实体</b>上）。
/// 使用地标时把抽到的牌记录到 <see cref="DrawnCard"/>；
/// 本回合内玩家打出这张牌 → 重新开启本地标（移除冷却并立即重新授予行动点，当回合即可再次使用）。
/// 回合结束时记录被清空（<see cref="Minions.TrinketShopLandmark"/> 处理），未在本回合打出则不再生效。
/// </summary>
[RegisterPower]
public sealed class TrinketTrackerPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 不可见（纯逻辑追踪，不显示图标）
    /// </summary>
    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// 使用地标时抽到的牌（本回合内打出它则重新开启地标）
    /// </summary>
    public CardModel? DrawnCard { get; set; }

    /// <summary>
    /// 玩家打出卡后：若是本回合抽到的那张牌，重新开启地标
    /// （移除冷却并立即重新授予使用行动点——当回合即可再次点击使用）。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var played = cardPlay?.Card;
        MegaCrit.Sts2.Core.Logging.Log.Info(
            $"[JainaTrinket] AfterCardPlayed: played={(played == null ? "null" : $"{played.Id}({played.GetType().Name})")} drawn={(DrawnCard == null ? "null" : $"{DrawnCard.Id}({DrawnCard.GetType().Name})")} same={played != null && played == DrawnCard}");
        if (DrawnCard == null || cardPlay.Card == null || cardPlay.Card != DrawnCard)
        {
            return;
        }
        DrawnCard = null;
        var owner = Owner;
        if (owner == null || owner.Monster is not Minions.JainaLandmarkBase landmark)
        {
            return;
        }
        await landmark.Reactivate(choiceContext);
    }
}
