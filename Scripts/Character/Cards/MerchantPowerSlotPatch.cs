using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 商店能力槽兼容补丁：当角色卡池没有任何能力卡（Power）时，
/// 将能力槽改为"随从槽"——按稀有度从随从卡中选卡（保留游戏的
/// 稀有度掷骰与 GetNextAllowedRarity 逻辑），避免商店因无 Power
/// 候选抛异常黑屏（吉安娜没有能力卡）。
/// 纯本地确定性逻辑，不影响多人联机同步。
/// </summary>
[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForMerchant),
    new[] { typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardType) })]
public static class MerchantPowerSlotPatch
{
    private static void Prefix(ref CardType type, IEnumerable<CardModel> options)
    {
        // 仅处理能力槽
        if (type != CardType.Power)
        {
            return;
        }
        // 候选中有能力卡时保留原逻辑（原版角色不受影响）
        if (options.Any(c => c.Type == CardType.Power))
        {
            return;
        }
        // 能力槽改为随从槽：按稀有度从随从卡（动态 Minion 类型）中选卡
        type = JainaCardTypes.Minion;
    }
}
