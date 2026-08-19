using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 自动打出（AutoPlay）目标补全：
/// 原版 <see cref="CardCmd.AutoPlay"/> 只对 TargetType.AnyEnemy/AnyAlly 自动选目标；
/// <b>自定义目标类型</b>（如 JainaTargetTypes.AnyTargetable——火球术/寒冰箭/埃匹希斯冲击等）
/// 的卡被自动打出（戏法图腾/浩劫 AutoPlayFromDrawPile/惊奇卡牌/匣中古神等）时 target 为 null
/// → cardPlay.Target 为 null → OnPlay 里 Targeting(null) NRE → 回合循环死亡、战斗卡死。
/// 这里在 Prefix 补全：自定义单目标类型且未指定目标时，按卡合法性自动随机选一个目标
/// （与 <see cref="JainaRandomPoolHelper.PickRandomTarget"/> 同一语义，联机两端同步确定性）。
/// </summary>
public static class AutoPlayTargetPatch
{
    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.AutoPlay))]
    public static class TargetPrefix
    {
        private static void Prefix(CardModel card, ref Creature? target)
        {
            if (card == null)
            {
                return;
            }
            // 实例标记：记录最近 AutoPlay 的卡（AutoPickSelectionPatch 用实例引用兜底判断
            // "当前选择是否来自随机释放"——调用栈检测在 async 包装下可能失效）。
            // 标记的清除由 OnPlayWrapper 统一处理（isAutoPlay=false 清空），
            // 玩家手打/地标使用等操作不会被残留标记误判。
            AutoPlayGuard.CurrentAutoPlayCard = card;
            if (target != null || card.TargetType == TargetType.None)
            {
                return;
            }
            // 原版已处理 AnyEnemy/AnyAlly，这里只补自定义目标类型
            if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyAlly)
            {
                return;
            }
            var combatState = card.CombatState ?? card.Owner?.Creature?.CombatState;
            if (combatState == null)
            {
                return;
            }
            target = JainaRandomPoolHelper.PickRandomTarget(card.Owner, combatState, card);
        }
    }

    /// <summary>
    /// 每个卡的 OnPlay 入口统一设置/清除 AutoPlay 实例标记：
    /// <c>isAutoPlay=true</c>（随机释放）→ 设置标记（其触发选择自动选）；
    /// <c>isAutoPlay=false</c>（玩家手打 PlayCardAction）→ 清空标记（手打触发选择正常弹界面）。
    /// 手打（isAutoPlay=false）时同时设置"吉安娜发起"标记：手打<b>吉安娜 mod 的卡</b>（匣中古神/
    /// 大法师的符文/重放类等）→ 其随机释放链（含原版/其它 mod 的卡）自动选；
    /// 手打原版/其它 mod 的卡 → 标记 false（其触发的选择正常弹界面，不影响其它 mod）。
    /// </summary>
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    public static class OnPlayWrapperContextPrefix
    {
        private static void Prefix(CardModel __instance, bool isAutoPlay)
        {
            AutoPlayGuard.CurrentAutoPlayCard = isAutoPlay ? __instance : null;
            if (!isAutoPlay)
            {
                // 手打：发起者 = 是否吉安娜 mod 的卡（吉安娜卡触发的随机释放链自动选）
                AutoPlayGuard.CurrentAutoPlayIsJainaOrigin = AutoPlayGuard.IsJainaCard(__instance);
            }
            // isAutoPlay=true（随机释放）：继承发起者标记（吉安娜发起的释放链保持 true，
            // 释放的卡无论是否吉安娜 mod 都自动选）
        }
    }

    /// <summary>
    /// 玩家手打（手动打出）入口：清空 AutoPlay 实例标记——
    /// 手打流程中触发选择（发现三选一/选牌等）时应正常弹界面等待玩家（不是随机释放）。
    /// （OnPlayWrapper(isAutoPlay=false) 已统一清空，此处双保险。）
    /// </summary>
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.TryManualPlay))]
    public static class ManualPlayClearPrefix
    {
        private static void Prefix()
        {
            AutoPlayGuard.CurrentAutoPlayCard = null;
        }
    }
}
