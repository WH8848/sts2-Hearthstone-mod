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
/// 用 Prefix 完全替换 PopulateCharacterCardEntries（该方法是私有实例方法，
/// 直接替换实现最简单；原版稀有度回退 GetNextAllowedRarity 逻辑在
/// CardFactory.CreateForMerchant(Player, IEnumerable, CardRarity) 中缺失，
/// 因此这里自行处理"掷出的稀有度无可售卡"的回退）。
/// </summary>
[HarmonyPatch(typeof(MerchantInventory), "PopulateCharacterCardEntries")]
public static class MerchantRarityOnlySlotPatch
{
    /// <summary>商店可售稀有度（Basic 被商店过滤，Ancient/Event/Token 等不在商店池）</summary>
    private static readonly CardRarity[] _merchantRarities =
    [
        CardRarity.Common,
        CardRarity.Uncommon,
        CardRarity.Rare
    ];

    private static bool Prefix(MerchantInventory __instance)
    {
        // 只对吉安娜生效：其他角色的商店保持原版按类型分槽逻辑
        if (__instance.Player?.Character is not jaina.Scripts.Character.Jaina)
        {
            return true; // 走原方法
        }
        var player = __instance.Player;
        var cardPool = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .ToList();

        // 5 个角色卡槽：各自掷 Shop 稀有度，从角色卡池按稀有度选卡（不限类型）
        int saleIndex = player.PlayerRng.Shops.NextInt(5);
        var alreadyTaken = new HashSet<CardModel>();
        for (int i = 0; i < 5; i++)
        {
            var entry = CreateEntryWithFallback(player, __instance, cardPool, alreadyTaken);
            AddEntry(__instance, entry);
            if (entry.CreationResult?.Card is { } chosen)
            {
                alreadyTaken.Add(chosen.CanonicalInstance);
            }
            if (saleIndex == i)
            {
                entry.SetOnSale();
            }
        }
        return false; // 跳过原方法
    }

    /// <summary>
    /// 掷稀有度并创建卡槽条目；若该稀有度的卡已被其他槽选走（Populate 内部
    /// Except(alreadyTaken) 后为空会抛错），回退到下一个有卡可选的稀有度重试。
    /// </summary>
    private static MerchantCardEntry CreateEntryWithFallback(Player player, MerchantInventory inventory,
        List<CardModel> cardPool, HashSet<CardModel> alreadyTaken)
    {
        var rarities = new List<CardRarity>();
        CardRarity rolled = player.PlayerOdds.CardRarity.RollWithoutChangingFutureOdds(CardRarityOddsType.Shop);
        rarities.Add(rolled);
        foreach (var rarity in _merchantRarities)
        {
            if (rarity != rolled)
            {
                rarities.Add(rarity);
            }
        }

        foreach (var rarity in rarities)
        {
            if (!cardPool.Any(c => c.Rarity == rarity))
            {
                continue;
            }
            var entry = new MerchantCardEntry(player, inventory, cardPool, rarity);
            try
            {
                entry.Populate();
            }
            catch
            {
                // 该稀有度的卡全被其他槽选走：尝试下一稀有度
                continue;
            }
            return entry;
        }
        // 防御：所有可售稀有度都无可用卡时，用掷出的稀有度交回原逻辑（会抛错但仅极端情况）
        var lastResort = new MerchantCardEntry(player, inventory, cardPool, rolled);
        lastResort.Populate();
        return lastResort;
    }

    /// <summary>
    /// 向 MerchantInventory 的私有 _characterCardEntries 列表添加条目
    /// </summary>
    private static readonly System.Reflection.FieldInfo CharacterEntriesField =
        typeof(MerchantInventory).GetField("_characterCardEntries",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

    private static void AddEntry(MerchantInventory inventory, MerchantCardEntry entry)
    {
        var list = (List<MerchantCardEntry>)CharacterEntriesField.GetValue(inventory)!;
        list.Add(entry);
    }
}
