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
            if (target != null || card == null || card.TargetType == TargetType.None)
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
}
