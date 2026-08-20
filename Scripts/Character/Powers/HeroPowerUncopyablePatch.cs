using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 英雄技能不可被复制：火焰冲击/二级火焰冲击/奥术爆裂/冰冷触摸/小精灵的祝福
/// （带 HeroPower 关键词的卡）不会被任何"复制卡牌"机制复制——
/// 英雄技能是英雄自带的，复制品会产生多张可无限使用的英雄技能（破坏平衡）。
/// 覆盖的原版复制入口：
/// 1. 佩尔的成长（PaelsGrowth.AfterObtained）：给牌库卡附 Clone 附魔——英雄技能卡
///    不被选中（附魔过滤）。营火克隆（CloneRestSiteOption）按 `Enchantment is Clone`
///    过滤，英雄技能卡永远没有 Clone 附魔 → 自然不会被营火复制；
/// 2. 音乐盒（MusicBox.BeforeCardPlayed）：打出攻击牌后复制——英雄技能是 Attack
///    类型，打出会被复制；拦截 BeforeCardPlayed 让英雄技能不进入复制标记。
/// mod 内部复制（模拟幻影/西瓦拉/幻觉药水/微缩/倒带等）已各自排除英雄技能卡。
/// </summary>
public static class HeroPowerUncopyablePatch
{
    /// <summary>
    /// 是否为英雄技能卡（复用 HeroPowerHandHelper 判定）
    /// </summary>
    private static bool IsHeroPower(CardModel? card)
    {
        return HeroPowerHandHelper.IsHeroPowerCard(card);
    }

    /// <summary>
    /// 佩尔的成长：附 Clone 附魔的候选排除英雄技能卡
    /// （英雄技能不可被复制 → 不可被选中复制）
    /// </summary>
    [HarmonyPatch(typeof(PaelsGrowth), nameof(PaelsGrowth.AfterObtained))]
    public static class PaelsGrowthPatch
    {
        private static bool Prefix(PaelsGrowth __instance)
        {
            // 用过滤后的牌库卡直接附 Clone 附魔（Amount=4，与原版一致）
            var owner = __instance.Owner;
            var candidates = PileType.Deck.GetPile(owner)?.Cards
                .Where(c => c != null && !IsHeroPower(c) && ModelDb.Enchantment<Clone>().CanEnchant(c))
                .ToList() ?? [];
            if (candidates.Count == 0)
            {
                return false;
            }
            // 随机选 1 张（与原版 FromDeckForEnchantment 1 张语义一致）
            var picked = owner.RunState.Rng.Niche.NextItem(candidates);
            if (picked == null)
            {
                return false;
            }
            CardCmd.Enchant<Clone>(picked, 4m);
            CardCmd.Preview(picked);
            return false;
        }
    }

    /// <summary>
    /// 音乐盒：打出英雄技能卡不复制（英雄技能不可被复制）。
    /// 拦截 BeforeCardPlayed：英雄技能卡不进入复制标记（_cardBeingPlayed）——
    /// AfterCardPlayed 中 `cardPlay.Card == CardBeingPlayed` 不成立 → 自然不复制，
    /// 且不残留状态（无副作用）。
    /// </summary>
    [HarmonyPatch(typeof(MusicBox), nameof(MusicBox.BeforeCardPlayed))]
    public static class MusicBoxPatch
    {
        private static bool Prefix(MusicBox __instance, CardPlay cardPlay)
        {
            // 英雄技能卡：跳过原版 BeforeCardPlayed（不设置复制标记）
            return !IsHeroPower(cardPlay.Card);
        }
    }

    /// <summary>
    /// 杂耍（JugglingPower）：本回合第 3 张攻击牌复制入手——英雄技能是 Attack
    /// 类型会被复制。拦截 BeforeCardPlayed：英雄技能卡不参与计数/不复制。
    /// </summary>
    [HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.JugglingPower), nameof(MegaCrit.Sts2.Core.Models.Powers.JugglingPower.BeforeCardPlayed))]
    public static class JugglingPatch
    {
        private static bool Prefix(MegaCrit.Sts2.Core.Models.Powers.JugglingPower __instance, CardPlay cardPlay)
        {
            return !IsHeroPower(cardPlay.Card);
        }
    }

    /// <summary>
    /// 噩梦（Nightmare）：从手牌任选一张复制 3 张——英雄技能在手牌中会被选中复制。
    /// 拦截 SetSelectedCard：英雄技能卡不进入复制目标（不复制）。
    /// </summary>
    [HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.NightmarePower), nameof(MegaCrit.Sts2.Core.Models.Powers.NightmarePower.SetSelectedCard))]
    public static class NightmarePatch
    {
        private static bool Prefix(MegaCrit.Sts2.Core.Models.Powers.NightmarePower __instance, CardModel card)
        {
            return !IsHeroPower(card);
        }
    }
}
