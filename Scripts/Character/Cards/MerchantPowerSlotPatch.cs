using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 商店能力槽兼容补丁：当角色卡池没有"商店可售稀有度"的能力卡（Power）时，
/// 将能力槽改为"随从槽"——按稀有度从随从卡中选卡（保留游戏的
/// 稀有度掷骰与 GetNextAllowedRarity 逻辑），避免商店因无有效 Power
/// 候选抛异常黑屏。
/// 注意：候选里即使有 Power 卡，若全是先古（Ancient）/Token 等不在商店
/// 稀有度池的卡，GetNextAllowedRarity 仍会抛 InvalidOperationException——
/// 因此按"可售稀有度"（Common/Uncommon/Rare/Shop）判断，而非只看类型。
/// <b>只对吉安娜生效</b>（其他角色保持原版行为，不受影响）。
/// 纯本地确定性逻辑，不影响多人联机同步。
/// </summary>
[HarmonyPatch(typeof(CardFactory), nameof(CardFactory.CreateForMerchant),
    new[] { typeof(Player), typeof(IEnumerable<CardModel>), typeof(CardType) })]
public static class MerchantPowerSlotPatch
{
    /// <summary>
    /// 商店稀有度池：掷骰与 GetNextAllowedRarity 只会命中这些稀有度
    /// （CardRarity 无 Shop 成员；Basic 被商店过滤，Ancient/Event/Token 等不在商店池）
    /// </summary>
    private static bool IsMerchantRarity(CardModel card)
    {
        return card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare;
    }

    private static void Prefix(Player player, ref CardType type, IEnumerable<CardModel> options)
    {
        // 只对吉安娜生效（其他角色保持原版商店逻辑）
        if (player?.Character is not jaina.Scripts.Character.Jaina)
        {
            return;
        }
        // 仅处理能力槽
        if (type != CardType.Power)
        {
            return;
        }
        // 候选中有"商店可售稀有度"的能力卡时保留原逻辑（原版角色不受影响）
        if (options.Any(c => c.Type == CardType.Power && IsMerchantRarity(c)))
        {
            return;
        }
        // 能力槽改为随从槽：按稀有度从随从卡（动态 Minion 类型）中选卡
        type = JainaCardTypes.Minion;
        // 防御：若随从卡可售候选也为空（如某稀有度缺卡），回退技能槽交给原逻辑，
        // 避免 CardFactory.GetNextAllowedRarity 走到 CardRarity.None 抛 InvalidOperationException
        if (!options.Any(c => c.Type == JainaCardTypes.Minion && IsMerchantRarity(c)))
        {
            type = CardType.Skill;
        }
    }
}
