using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 「变形卷轴」的回合开始变形驱动。
///
/// 游戏 CardModel 只有 OnTurnEndInHand（回合结束在手牌）钩子，没有回合开始钩子，
/// 因此这里包装 Hook.AfterSideTurnStart（回合开始抽牌之后触发）：等原钩子链执行完后，
/// 遍历该侧玩家的手牌，把每一张变形卷轴变形为随机法师法术牌。
///
/// 联机：Hook 在两端按相同顺序执行，随机数用 RunState.Rng，变形结果一致。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterSideTurnStart))]
public static class ShiftingScrollTurnStartPatch
{
    private static void Postfix(ref Task __result, ICombatState combatState, CombatSide side, IReadOnlyList<Creature> participants)
    {
        __result = WrapAsync(__result, combatState, side);
    }

    private static async Task WrapAsync(Task original, ICombatState combatState, CombatSide side)
    {
        await original;
        if (combatState == null || side != CombatSide.Player)
        {
            return;
        }
        try
        {
            foreach (var player in combatState.Players)
            {
                var hand = player?.PlayerCombatState?.Hand;
                if (hand == null || player!.Creature?.Side != side)
                {
                    continue;
                }
                var scrolls = hand.Cards
                    .Where(c => c != null
                                && (c.GetType() == typeof(ShiftingScrollCard)
                                    || c.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.ShiftingMark)))
                    .ToList();
                foreach (var scroll in scrolls)
                {
                    try
                    {
                        await ShiftingScrollCard.TransformNow(scroll, player);
                    }
                    catch (System.Exception ex)
                    {
                        MegaCrit.Sts2.Core.Logging.Log.Error(
                            $"[Jaina] Shifting Scroll transform failed: {ex}");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] Shifting Scroll turn-start hook failed: {ex}");
        }
    }
}
