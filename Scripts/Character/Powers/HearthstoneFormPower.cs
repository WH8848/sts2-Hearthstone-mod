using System;
using System.Collections.Generic;
using System.Linq;
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
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 炉石形态：你的全部卡牌获得保留和消耗；当你抽到状态卡时额外抽一张。
/// 此后每回合你获得十点能量，回合开始抽五张卡变为抽一张卡。
/// 当你手牌上限再抽牌时，抽到的牌会被消耗。
/// 当你抽牌堆和弃牌堆无牌可抽时，进入疲劳（第1次扣1血，第2次扣2血，以此类推）。
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

    /// <summary>
    /// 疲劳计数（本局对战）：无牌可抽时第1次扣1血、第2次扣2血……
    /// </summary>
    private int _fatigueCount;

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

    /// <summary>
    /// 抽牌拦截（炉石形态激活时）：
    /// - 手牌已达上限且有牌可抽 → 拦截，触发"烧牌"（抽到的牌被消耗，见 <see cref="AfterPreventingDraw"/>）；
    /// - 抽牌堆和弃牌堆都无牌可抽 → 拦截，触发疲劳（扣血递增）。
    /// </summary>
    public override bool ShouldDraw(Player player, bool fromHandDraw)
    {
        if (player != Owner?.Player || player.PlayerCombatState == null)
        {
            return true;
        }
        var drawPile = player.PlayerCombatState.DrawPile;
        var discardPile = player.PlayerCombatState.DiscardPile;
        // 手牌上限（非英雄技能卡 ≥ 10）且有牌可抽 → 烧牌
        bool handFull = Powers.HeroPowerHandHelper.GetNonHeroPowerCardCountFromPile(
            player.PlayerCombatState.Hand) >= CardPile.MaxCardsInHand;
        if (handFull && drawPile.Cards.Count > 0)
        {
            return false;
        }
        // 抽牌堆与弃牌堆都无牌 → 疲劳
        if (drawPile.Cards.Count == 0 && discardPile.Cards.Count == 0)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 抽牌被拦截后：
    /// - 手牌满且有牌可抽 → 抽牌堆顶牌被消耗（烧牌，牌被摧毁）；
    /// - 两堆无牌 → 疲劳：直接失去生命（无视护甲与挡伤），第 N 次扣 N 点。
    /// </summary>
    public override async Task AfterPreventingDraw()
    {
        var player = Owner?.Player;
        if (player == null || player.PlayerCombatState == null)
        {
            return;
        }
        var drawPile = player.PlayerCombatState.DrawPile;
        // 手牌满且有牌可抽 → 烧牌：抽牌堆顶牌被消耗（从牌库移除销毁）
        bool handFull = Powers.HeroPowerHandHelper.GetNonHeroPowerCardCountFromPile(
            player.PlayerCombatState.Hand) >= CardPile.MaxCardsInHand;
        if (handFull && drawPile.Cards.Count > 0)
        {
            var card = drawPile.Cards.FirstOrDefault();
            if (card != null)
            {
                card.RemoveFromCurrentPile(silent: true);
                MegaCrit.Sts2.Core.Logging.Log.Info(
                    $"[JainaFatigue] hand full, burned: {card.Id}");
            }
            return;
        }
        // 两堆无牌 → 疲劳：直接扣血（绕过护甲/挡伤，炉石"失去生命"语义）
        _fatigueCount++;
        int damage = _fatigueCount;
        MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaFatigue] fatigue damage: {damage}");
        Owner.LoseHpInternal(damage, ValueProp.Unpowered);
        if (Owner.IsDead)
        {
            await CreatureCmd.Kill(Owner, force: true);
        }
    }
}
