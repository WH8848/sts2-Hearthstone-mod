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
using MegaCrit.Sts2.Core.Runs;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 英雄技能不可被复制：火焰冲击/二级火焰冲击/奥术爆裂/冰冷触摸/小精灵的祝福
/// （带 HeroPower 关键词的卡）不会被任何"复制卡牌"机制复制——
/// 英雄技能是英雄自带的，复制品会产生多张可无限使用的英雄技能（破坏平衡）。
/// 覆盖的复制入口：
/// <b>战斗内打出复制</b>（音乐盒/杂耍/噩梦，见各 Prefix）；
/// <b>牌库复制</b>（事件倒影 Reflections 复制整个牌库、遗物 BingBong 进牌库复制、
/// 多莉的镜子 DollysMirror 选牌库卡复制）——统一在 <see cref="RunState.CloneCard"/>
/// 拦截（返回 null），并让 <see cref="CardPileCmd.Add(CardModel, PileType, ...)"/>
/// 容忍 null 卡（跳过添加，不产生复制品）；DollysMirror 选择界面过滤英雄技能卡；
/// <b>历史课 HistoryCourse</b>（复制上一回合打出的攻击牌，英雄技能是 Attack 类型）拦截。
/// PaelsGrowth（Clone 附魔源）过滤英雄技能卡——营火克隆天然安全（无 Clone 附魔）。
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
    /// 牌库复制统一拦截：RunState.CloneCard 对英雄技能卡返回 null——
    /// 覆盖事件倒影（Reflections.Shatter 复制整个牌库）、遗物 BingBong
    /// （卡进入牌库时复制）、多莉的镜子（DollysMirror 选牌库卡复制）、
    /// 冰蛋/熔岩蛋/毒蛋（仅 Power 类型，天然不触发）、弗雷斯内尔透镜/
    /// 闪光/丝绒发辫/熔岩灯/白银坩埚/羽饰（奖励卡附魔/升级克隆，天然不含英雄技能）。
    /// </summary>
    [HarmonyPatch(typeof(RunState), nameof(RunState.CloneCard))]
    public static class RunStateCloneCardPatch
    {
        private static bool Prefix(RunState __instance, CardModel mutableCard, ref CardModel? __result)
        {
            if (IsHeroPower(mutableCard))
            {
                __result = null;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 容忍 null 卡（英雄技能复制被拦截后 Add(null) 不应抛异常）：
    /// null 卡直接返回默认结果（不添加任何牌）。
    /// </summary>
    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add),
        new[] { typeof(CardModel), typeof(PileType), typeof(CardPilePosition), typeof(AbstractModel), typeof(bool) })]
    public static class CardPileAddNullPatch
    {
        private static bool Prefix(CardModel card, ref Task<CardPileAddResult> __result)
        {
            if (card != null)
            {
                return true;
            }
            __result = Task.FromResult(new CardPileAddResult());
            return false;
        }
    }

    /// <summary>
    /// 多莉的镜子（DollysMirror）：从牌库选卡复制——英雄技能卡不出现在选择候选中
    /// （Filter 追加排除；选到 null 卡也不复制）。
    /// </summary>
    [HarmonyPatch(typeof(DollysMirror), "Filter")]
    public static class DollysMirrorFilterPatch
    {
        private static bool Prefix(DollysMirror __instance, CardModel c, ref bool __result)
        {
            if (IsHeroPower(c))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 历史课（HistoryCourse）：复制你上一回合打出的最后一张攻击牌——
    /// 英雄技能是 Attack 类型会被复制。拦截：上一张攻击牌是英雄技能则不复制。
    /// （async 方法：Prefix 返回 false 跳过原方法时需补 __result = CompletedTask，
    /// 否则返回 null Task 导致调用方 NRE——同 TemporaryPowerPetTurnEndFix 模式。）
    /// </summary>
    [HarmonyPatch(typeof(HistoryCourse), nameof(HistoryCourse.AfterAutoPrePlayPhaseEntered))]
    public static class HistoryCoursePatch
    {
        private static bool Prefix(HistoryCourse __instance, ref Task __result)
        {
            var player = __instance.Owner;
            if (player != null && player.PlayerCombatState?.TurnNumber != 1)
            {
                var last = MegaCrit.Sts2.Core.Combat.CombatManager.Instance.History.CardPlaysFinished
                    .LastOrDefault(e => e.CardPlay.Player == player &&
                                        e.HappenedLastPlayerTurn(player) &&
                                        e.CardPlay.Card.Type == CardType.Attack &&
                                        !e.CardPlay.Card.IsDupe);
                if (last?.CardPlay.Card != null && IsHeroPower(last.CardPlay.Card))
                {
                    __result = Task.CompletedTask;
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 佩尔的成长：附 Clone 附魔的候选排除英雄技能卡
    /// （英雄技能不可被复制 → 不可被选中复制）。
    /// <b>注意</b>：原方法是 async Task，Prefix 跳过时必须补 __result=CompletedTask
    /// （否则 null Task 在 AfterObtained 调用处 await NRE，同 MusicBoxPatch 注释）。
    /// </summary>
    [HarmonyPatch(typeof(PaelsGrowth), nameof(PaelsGrowth.AfterObtained))]
    public static class PaelsGrowthPatch
    {
        private static bool Prefix(PaelsGrowth __instance, ref Task __result)
        {
            // 用过滤后的牌库卡直接附 Clone 附魔（Amount=4，与原版一致）
            var owner = __instance.Owner;
            var candidates = PileType.Deck.GetPile(owner)?.Cards
                .Where(c => c != null && !IsHeroPower(c) && ModelDb.Enchantment<Clone>().CanEnchant(c))
                .ToList() ?? [];
            if (candidates.Count == 0)
            {
                __result = Task.CompletedTask;
                return false;
            }
            // 随机选 1 张（与原版 FromDeckForEnchantment 1 张语义一致）
            var picked = owner.RunState.Rng.Niche.NextItem(candidates);
            if (picked == null)
            {
                __result = Task.CompletedTask;
                return false;
            }
            CardCmd.Enchant<Clone>(picked, 4m);
            CardCmd.Preview(picked);
            __result = Task.CompletedTask;
            return false;
        }
    }

    /// <summary>
    /// 音乐盒：打出英雄技能卡不复制（英雄技能不可被复制）。
    /// 拦截 BeforeCardPlayed：英雄技能卡不进入复制标记（_cardBeingPlayed）——
    /// AfterCardPlayed 中 `cardPlay.Card == CardBeingPlayed` 不成立 → 自然不复制，
    /// 且不残留状态（无副作用）。
    /// <b>注意</b>：原方法是返回 Task 的方法（非 async 也返回 Task），Prefix 跳过
    /// 时必须补 <c>__result = Task.CompletedTask</c>——否则 __result 默认 null Task，
    /// Hook.BeforeCardPlayed 的 await 对 null 抛 NullReferenceException，
    /// 英雄技能打出即崩（无伤害、进弃牌堆）。同 TemporaryPowerPetTurnEndFix 模式。
    /// </summary>
    [HarmonyPatch(typeof(MusicBox), nameof(MusicBox.BeforeCardPlayed))]
    public static class MusicBoxPatch
    {
        private static bool Prefix(MusicBox __instance, CardPlay cardPlay, ref Task __result)
        {
            // 英雄技能卡：跳过原版 BeforeCardPlayed（不设置复制标记）
            if (IsHeroPower(cardPlay.Card))
            {
                __result = Task.CompletedTask;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 杂耍（JugglingPower）：本回合第 3 张攻击牌复制入手——英雄技能是 Attack
    /// 类型会被复制。拦截 BeforeCardPlayed：英雄技能卡不参与计数/不复制。
    /// <b>注意</b>：原方法是 async Task，Prefix 跳过时必须补 __result=CompletedTask
    /// （否则 null Task → Hook.BeforeCardPlayed await NRE，见 MusicBoxPatch 注释）。
    /// </summary>
    [HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.JugglingPower), nameof(MegaCrit.Sts2.Core.Models.Powers.JugglingPower.BeforeCardPlayed))]
    public static class JugglingPatch
    {
        private static bool Prefix(MegaCrit.Sts2.Core.Models.Powers.JugglingPower __instance, CardPlay cardPlay, ref Task __result)
        {
            if (IsHeroPower(cardPlay.Card))
            {
                __result = Task.CompletedTask;
                return false;
            }
            return true;
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
