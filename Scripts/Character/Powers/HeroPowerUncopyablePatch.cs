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
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
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
    /// UI 预览/选择上下文：为 true 时 <see cref="RunState.CloneCard"/> 对英雄技能卡<b>正常克隆</b>（不拦截）——
    /// 升级预览（NUpgradePreview/NDeckUpgradeSelectScreen）与附魔预览（NEnchantPreview/
    /// 使用预览需要克隆一张卡来渲染"升级后/附魔后"卡面；若拦截返回 null，
    /// 预览调用 NullReferenceException（实测：锻造界面点击二级火焰冲击卡住）。
    /// 克隆拦截只应用于真正的复制机制（倒影/叮当/多莉的镜子/历史课等）。
    /// 由下方 UI patch 的 Prefix/Postfix 置位/复位（UI 线程同步，无并发）。
    /// </summary>
    internal static bool UiPreviewCloneContext;

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
    /// <b>UI 预览克隆（升级/附魔预览）放行</b>：见 <see cref="UiPreviewCloneContext"/>。
    /// </summary>
    [HarmonyPatch(typeof(RunState), nameof(RunState.CloneCard))]
    public static class RunStateCloneCardPatch
    {
        private static bool Prefix(RunState __instance, CardModel mutableCard, ref CardModel? __result)
        {
            if (IsHeroPower(mutableCard) && !HeroPowerUncopyablePatch.UiPreviewCloneContext)
            {
                __result = null;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 升级预览（单卡模式，NUpgradePreview.Reload 经 Card.CardScope.CloneCard 克隆）：
    /// 预览期间克隆放行（否则英雄技能卡升级预览 NRE，联网锻造界面点击卡住）。
    /// </summary>
    [HarmonyPatch(typeof(NUpgradePreview), "Reload")]
    public static class NUpgradePreviewReloadPatch
    {
        private static void Prefix() => UiPreviewCloneContext = true;

        private static void Postfix() => UiPreviewCloneContext = false;
    }

    /// <summary>
    /// 升级选择屏（多选模式，OnCardClicked 内 _runState.CloneCard 克隆）：
    /// 选择/预览期间克隆放行。
    /// </summary>
    [HarmonyPatch(typeof(NDeckUpgradeSelectScreen), "OnCardClicked")]
    public static class NDeckUpgradeSelectScreenPatch
    {
        private static void Prefix() => UiPreviewCloneContext = true;

        private static void Postfix() => UiPreviewCloneContext = false;
    }

    /// <summary>
    /// 附魔预览（NEnchantPreview.Init 经 Card.CardScope.CloneCard 克隆）：
    /// 预览期间克隆放行。
    /// </summary>
    [HarmonyPatch(typeof(NEnchantPreview), nameof(NEnchantPreview.Init))]
    public static class NEnchantPreviewPatch
    {
        private static void Prefix() => UiPreviewCloneContext = true;

        private static void Postfix() => UiPreviewCloneContext = false;
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
    /// (已移除 PaelsGrowth.Prefix——原实现把"玩家从牌库自选一张卡附 Clone 附魔"
    /// 改成了随机选 1 张自动附魔,导致"只能克隆卡组里的一张卡,其他卡没附魔也没法选择"。
    /// 现恢复原版玩家选择流程;英雄技能卡排除改由 CanEnchant 层拦截
    /// (见 LandmarkEnchantBlockPatch.EnchantmentModel.CanEnchant 的 Clone+英雄技能 分支)。
    /// </summary>

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
