using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
/// 该卡召唤出随从后（随从 OnSummon，战吼前），玩家获得 2*层数 力量，
/// 给召唤出的随从挂 <see cref="AegwynnInheritedPower"/>（死亡时返还力量并链式传续），
/// 随后本 Power 移除。
/// <b>层数（Amount）= 死亡过的艾格文数量</b>（Counter 叠层）：两张艾格文亡语
/// 各自+1 层 → 继承随从获得 +4 力量、挂 2 层继承（死亡时一次返还 4 点并传 2 层）。
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

    /// <summary>
    /// 可叠层：每张死亡的艾格文 +1 层（两张艾格文 → 继承 2 层）
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 已标记继承能力的随从牌实例
    /// </summary>
    private CardModel? _claimedCard;

    /// <summary>
    /// 已标记继承能力的随从牌实例（支持多张：模拟幻影复制标记卡时同步给复制品；
    /// 任意一张打出兑现继承后全部作废——继承预算只兑现一次）
    /// </summary>
    private readonly HashSet<CardModel> _claimedCards = new();

    /// <summary>
    /// 该卡是否是被标记的"下一张随从牌"（卡面显示艾格文亡语提示用）
    /// </summary>
    public bool IsClaimedCard(CardModel card) => _claimedCards.Contains(card);

    /// <summary>
    /// 抽到随从牌时标记（只标记第一张；匹配标记卡由 OnSummon 消费转移后清空）
    /// </summary>
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (_claimedCards.Count == 0 && card is JainaMinionCardTemplate)
        {
            _claimedCards.Add(card);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 复制标记卡时同步给复制品（模拟幻影）：
    /// 原卡与复制品共享"下一次兑现"的继承预算，任意一张打出即转移并全部作废。
    /// </summary>
    public void ClaimCopy(CardModel sourceCard, CardModel copy)
    {
        if (_claimedCards.Contains(sourceCard))
        {
            _claimedCards.Add(copy);
        }
    }

    /// <summary>
    /// 被标记的随从牌召唤随从后调用（随从登场、战吼之前）：
    /// 玩家获得 2*层数 力量；召唤出的随从挂继承效果（死亡后返还力量并继续传递）；
    /// 本 Power 移除。层数=死亡过的艾格文数（两张艾格文 → +4 力量与 2 层继承）。
    /// </summary>
    public async Task ConsumeForMinion(PlayerChoiceContext choiceContext, Creature minion, CardModel card)
    {
        if (!_claimedCards.Contains(card))
        {
            return;
        }
        _claimedCards.Clear();
        int count = System.Math.Max(1, (int)Amount);
        // 该卡在 OnSummon 中记录召唤出的随从生物（要求随从成功站场才算继承）
        if (minion is not { IsAlive: true })
        {
            return;
        }
        await PowerCmd.Apply<StrengthPower>(choiceContext, [Owner], 2m * count, Owner, null);
        // 链式传递：该随从死亡时返还 2*层数 力量并把继承效果传给下一张随从
        await PowerCmd.Apply<AegwynnInheritedPower>(choiceContext, [minion], count, minion, null);
        await PowerCmd.Remove(this);
    }
}
