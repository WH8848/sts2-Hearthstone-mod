using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 炉石形态：你的全部卡牌获得保留和消耗；当你抽到状态卡时额外抽一张。
/// 此后每回合你获得十点能量，每回合只能抽一张卡。
/// 挂在吉安娜玩家身上（可见）。
/// </summary>
[RegisterPower]
public sealed class HearthstoneFormPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_hearthstone_form_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 你的全部卡牌获得保留和消耗（对每张卡贡献全局关键词，无需清理）
    /// </summary>
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
    {
        keywords.Add(CardKeyword.Retain);
        keywords.Add(CardKeyword.Exhaust);
        return true;
    }

    /// <summary>
    /// 每回合只能抽一张卡：回合开始初始抽牌数改为 1（替代默认 5 张）。
    /// 注意：初始抽牌是单次 Draw(5) 调用，ShouldDraw 只能拦截整次抽牌，
    /// 必须用 ModifyHandDraw 修改抽牌数才能按张生效。
    /// </summary>
    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player == Owner?.Player)
        {
            return 1m;
        }
        return count;
    }

    /// <summary>
    /// 玩家回合开始后：每回合获得十点能量（避免被默认能量重置覆盖）
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner?.Player && player.PlayerCombatState != null)
        {
            player.PlayerCombatState.Energy = 10;
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// 抽牌后：抽到状态卡时额外抽一张（可能连锁）
    /// </summary>
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        var player = Owner?.Player;
        if (player == null || card.Owner != player)
        {
            return;
        }
        if (card.Type == CardType.Status)
        {
            // 抽到状态卡：额外抽一张（可能连锁）
            await CardPileCmd.Draw(choiceContext, 1, player);
        }
        await Task.CompletedTask;
    }
}
