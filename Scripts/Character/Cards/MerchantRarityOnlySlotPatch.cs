using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 商店角色卡槽补丁：角色卡位不再按卡牌类型分槽（原版固定
/// [Attack, Attack, Skill, Skill, Power] 5 个类型槽），改为只按稀有度
/// 从角色卡池中选卡（与原版无色卡槽同一机制：掷 Shop 稀有度 → 按稀有度取卡，
/// 不限类型）。无色卡槽保留原逻辑。
/// <b>只对吉安娜生效</b>（其他角色的商店保持原版按类型分槽逻辑，不受影响）。
/// <para>
/// 0.113 适配：原版删除了 <c>MerchantInventory.PopulateCharacterCardEntries</c>，
/// 改为 <c>GetDefaultCharacterCardEntries(Player)</c>（private static，返回 5 个按类型的
/// <c>MerchantCardEntry</c>，真正的选卡在 <c>Setup()</c> 里逐个 <c>Populate(inventory)</c>）。
/// 因此这里改为 Prefix 替换该工厂方法：按稀有度创建 5 个 entry，Populate 仍由 Setup 负责
/// （blacklist 会自动排除已选中的卡，无需手动跟踪）。
/// </para>
/// </summary>
[HarmonyPatch(typeof(MerchantInventory), "GetDefaultCharacterCardEntries")]
public static class MerchantRarityOnlySlotPatch
{
    /// <summary>商店可售稀有度（Basic 被商店过滤，Ancient/Event/Token 等不在商店池）</summary>
    private static readonly CardRarity[] _merchantRarities =
    [
        CardRarity.Common,
        CardRarity.Uncommon,
        CardRarity.Rare
    ];

    private static bool Prefix(Player player, ref List<MerchantCardEntry> __result)
    {
        // 只对吉安娜生效：其他角色的商店保持原版按类型分槽逻辑
        if (player?.Character is not jaina.Scripts.Character.Jaina)
        {
            return true; // 走原方法
        }
        var cardPool = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .ToList();

        // 5 个角色卡槽：各自掷 Shop 稀有度，从角色卡池按稀有度选卡（不限类型）
        var entries = new List<MerchantCardEntry>();
        for (int i = 0; i < 5; i++)
        {
            entries.Add(new MerchantCardEntry(player, cardPool, PickRarityWithCards(player, cardPool)));
        }
        __result = entries;
        return false; // 跳过原方法
    }

    /// <summary>
    /// 掷 Shop 稀有度；若该稀有度在卡池里没有可售卡，回退到下一个有卡可选的稀有度，
    /// 避免 0.113 <c>Setup → Populate</c> 时 <c>CardFactory.GetNextAllowedRarity</c>
    /// 走到 CardRarity.None 抛 InvalidOperationException。
    /// </summary>
    private static CardRarity PickRarityWithCards(Player player, List<CardModel> cardPool)
    {
        CardRarity rolled = player.PlayerOdds.CardRarity.RollWithoutChangingFutureOdds(CardRarityOddsType.Shop);
        if (cardPool.Any(c => c.Rarity == rolled))
        {
            return rolled;
        }
        foreach (var rarity in _merchantRarities)
        {
            if (cardPool.Any(c => c.Rarity == rarity))
            {
                return rarity;
            }
        }
        return CardRarity.Common;
    }
}
