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
/// 本回合内玩家打出这张牌 → 重新开启本地标（移除冷却，下一回合仍可用）。
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
    /// 玩家打出卡后：若是本回合抽到的那张牌，重新开启地标（移除冷却）。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (DrawnCard == null || cardPlay.Card == null || cardPlay.Card != DrawnCard)
        {
            return;
        }
        DrawnCard = null;
        var owner = Owner;
        if (owner == null)
        {
            return;
        }
        var cooldown = owner.GetPower<LandmarkCooldownPower>();
        if (cooldown != null)
        {
            await PowerCmd.Remove(cooldown);
        }
        if (owner.Monster is Minions.JainaLandmarkBase landmark)
        {
            landmark.RefreshIntentDisplay();
        }
    }
}
