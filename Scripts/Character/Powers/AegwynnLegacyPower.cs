using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 守护者艾格文的亡语：你抽到的下一张随从牌会继承"力量光环+2"。
/// 挂在玩家身上：抽到随从牌（JainaMinionCardTemplate）时记录该卡实例；
/// 该卡打出并召唤随从后，把 AegwynnAuraPower 转移到新召唤的随从身上，本 Power 移除。
/// </summary>
[RegisterPower]
public sealed class AegwynnLegacyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 已标记继承能力的随从牌实例
    /// </summary>
    private CardModel? _claimedCard;

    /// <summary>
    /// 抽到随从牌时标记（只标记第一张）
    /// </summary>
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (_claimedCard == null && card is JainaMinionCardTemplate)
        {
            _claimedCard = card;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 被标记的随从牌打出并召唤随从后：力量光环转移到新随从身上，本 Power 移除
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_claimedCard == null || !ReferenceEquals(_claimedCard, cardPlay.Card))
        {
            return;
        }
        _claimedCard = null;

        // 该卡在 OnPlay 中记录了召唤出的随从生物
        if (cardPlay.Card is JainaMinionCardTemplate { LastSummonedMinion: { } minion })
        {
            await PowerCmd.Apply<AegwynnAuraPower>(choiceContext, [minion], 2m, minion, null);
        }
        await PowerCmd.Remove(this);
    }
}
