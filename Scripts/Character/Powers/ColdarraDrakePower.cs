using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 考达拉幼龙光环（挂在随从身上，随从死亡自动失效）：
/// - 你的英雄技能变为1费（战斗中费用解析固定为 1）；
/// - 你可以使用任意次数的英雄技能（手打的英雄技能卡打出后回到手牌；
///   自动打出（小精灵驾驭者等）不回手）。
/// </summary>
[RegisterPower]
public sealed class ColdarraDrakePower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_coldarra_drake_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 你的英雄技能变为1费：战斗中费用解析时，主人（随从的主人）的英雄技能卡费用固定为 1。
    /// 注意：Power 挂在随从身上，Owner.Player 对随从为 null——必须用 Owner.PetOwner
    /// （随从的主人），否则 1 费与回手都不生效（实测：playerNull=True）。
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (Owner == null || !Owner.IsAlive)
        {
            return false;
        }
        var player = Owner.PetOwner;
        if (player == null || card.Owner != player)
        {
            return false;
        }
        // 只修改英雄技能卡（火焰冲击/二级火焰冲击/奥术爆裂/冰冷触摸等）
        if (!HeroPowerHandHelper.IsHeroPowerCard(card))
        {
            return false;
        }
        modifiedCost = 1;
        return true;
    }

    /// <summary>
    /// 你可以使用任意次数的英雄技能：玩家手打的英雄技能卡打出后回到手牌
    /// （自动打出——小精灵驾驭者/鲁莽的学徒的副本——不回手，避免副本滞留）。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner == null || !Owner.IsAlive)
        {
            MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDiag] Coldarra AfterCardPlayed: ownerNull={Owner == null} alive={Owner?.IsAlive}");
            return;
        }
        var player = Owner.PetOwner;
        if (player == null || cardPlay.Card.Owner != player)
        {
            MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDiag] Coldarra AfterCardPlayed: playerNull={player == null} cardOwner={cardPlay.Card.Owner?.NetId} player={player?.NetId}");
            return;
        }
        var card = cardPlay.Card;
        // 只响应英雄技能卡
        if (!HeroPowerHandHelper.IsHeroPowerCard(card))
        {
            MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDiag] Coldarra AfterCardPlayed: {card.Id.Entry} NOT heroPower");
            return;
        }
        // 自动打出（IsAutoPlay）不回手
        if (cardPlay.IsAutoPlay)
        {
            MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDiag] Coldarra AfterCardPlayed: {card.Id.Entry} isAutoPlay skip");
            return;
        }
        // 已在手牌（如打出前已在手）不重复入手
        if (card.Pile != null && card.Pile.Type == PileType.Hand)
        {
            MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDiag] Coldarra AfterCardPlayed: {card.Id.Entry} already in hand skip");
            return;
        }
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaDiag] Coldarra AfterCardPlayed: {card.Id.Entry} returning to hand from pile={card.Pile?.Type}");
        await CardPileCmd.Add(card, PileType.Hand);
    }
}
