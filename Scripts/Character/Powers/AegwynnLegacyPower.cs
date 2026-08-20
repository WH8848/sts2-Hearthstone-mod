using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 守护者艾格文的亡语：你抽到的下一张随从牌会继承此效果（力量+2 与亡语）。
/// 挂在玩家身上：抽到随从牌（JainaMinionCardTemplate）时记录该卡实例；
/// 该卡打出并召唤随从后，给玩家施加 2 层力量，并给召唤出的随从挂
/// <see cref="AegwynnInheritedPower"/>（该随从死亡时移除 +2 力量并继续传递
/// 给下一张随从，链式继承），随后本 Power 移除。
/// </summary>
[RegisterPower]
public sealed class AegwynnLegacyPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_aegwynn_legacy_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 已标记继承能力的随从牌实例
    /// </summary>
    private CardModel? _claimedCard;

    /// <summary>
    /// 该卡是否是被标记的"下一张随从牌"（卡面显示艾格文亡语提示用）
    /// </summary>
    public bool IsClaimedCard(CardModel card) => ReferenceEquals(_claimedCard, card);

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
    /// 被标记的随从牌打出并召唤随从后：
    /// 玩家获得力量+2，召唤出的随从挂继承效果（死亡后继续传递），本 Power 移除。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_claimedCard == null || !ReferenceEquals(_claimedCard, cardPlay.Card))
        {
            return;
        }
        _claimedCard = null;

        // 该卡在 OnPlay 中记录了召唤出的随从生物（要求随从成功站场才算继承）
        if (cardPlay.Card is JainaMinionCardTemplate { LastSummonedMinion: { IsAlive: true } minion })
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, [Owner], 2m, Owner, null);
            // 链式传递：该随从死亡时移除 +2 力量并把继承效果传给下一张随从
            await PowerCmd.Apply<AegwynnInheritedPower>(choiceContext, [minion], 1m, minion, null);
        }
        await PowerCmd.Remove(this);
    }
}

