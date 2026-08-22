using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 模拟幻影复制品的艾格文继承（独立类型，与主链
/// <see cref="AegwynnLegacyPower"/> 互不干扰）：
/// 被标记的随从牌被模拟幻影复制时，复制品获得<b>一次独立</b>的继承机会——
/// 打出复制品：玩家获得 2*层数 力量，随从挂 <see cref="AegwynnInheritedPower"/>
/// （死亡返还并链上传续），本 Power 移除。
/// 使用独立类型是因为同类型 Power 单实例（Counter 叠层会在同一实例上累加），
/// 无法同时承载"主链等待中的下一张"与"复制品未兑现机会"；独立类型彻底分离两条链。
/// </summary>
[RegisterPower]
public sealed class AegwynnLegacyCopyPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_aegwynn_legacy_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 可叠层：死亡过的艾格文数量（与主链同层数）
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 被标记的复制品（模拟幻影新建的卡实例）
    /// </summary>
    private CardModel? _claimedCard;

    /// <summary>
    /// 该复制品是否被标记（卡面显示艾格文亡语提示用）
    /// </summary>
    public bool IsClaimedCard(CardModel card) => ReferenceEquals(_claimedCard, card);

    /// <summary>
    /// 记录复制品为标记卡（由主链 ClaimCopyAsync 调用）
    /// </summary>
    public void MarkClaimed(CardModel card) => _claimedCard = card;

    /// <summary>
    /// 被标记的复制品召唤随从后调用（随从登场、战吼之前）：
    /// 玩家获得 2*层数 力量；召唤出的随从挂继承效果；本 Power 移除。
    /// </summary>
    public async Task ConsumeForMinion(PlayerChoiceContext choiceContext, Creature minion, CardModel card)
    {
        if (!ReferenceEquals(_claimedCard, card))
        {
            return;
        }
        _claimedCard = null;
        int count = System.Math.Max(1, (int)Amount);
        if (minion is not { IsAlive: true })
        {
            return;
        }
        await PowerCmd.Apply<StrengthPower>(choiceContext, [Owner], 2m * count, Owner, null);
        await PowerCmd.Apply<AegwynnInheritedPower>(choiceContext, [minion], count, minion, null);
        await PowerCmd.Remove(this);
    }
}
