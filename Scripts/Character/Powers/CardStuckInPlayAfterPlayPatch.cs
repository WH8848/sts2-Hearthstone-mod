using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 打出后兜底清理：原版 MoveToResultPileAfterPlay 在边界情况下会跳过
/// （战斗结束 IsOverOrEnding / 玩家死亡 / 卡不在打出区等）→ 卡牌滞留打出区，
/// 模型与 UI 节点悬在空中（实测：强能奥术飞弹基础版打出后卡在空中、无异常、
/// 其他卡仍可操作）。OnPlayWrapper 执行完后若卡仍在打出区（原版没移动）→
/// 兜底移入弃牌堆（正常情况原版已移动，Pile 不是 Play，不会触发）。
/// </summary>
[HarmonyPatch]
public static class CardStuckInPlayAfterPlayPatch
{
    private static MethodBase TargetMethod()
    {
        // OnPlayWrapper(PlayerChoiceContext, Creature, bool, ResourceInfo, bool)
        return AccessTools.Method(typeof(CardModel), "OnPlayWrapper",
            new[]
            {
                typeof(PlayerChoiceContext),
                typeof(Creature),
                typeof(bool),
                typeof(ResourceInfo),
                typeof(bool)
            });
    }

    public static void Postfix(CardModel __instance, ref Task __result)
    {
        __result = WrapAsync(__instance, __result);
    }

    private static async Task WrapAsync(CardModel card, Task original)
    {
        try
        {
            await original;
        }
        finally
        {
            // 兜底：战斗进行中且卡仍滞留打出区（原版移动被跳过）→ 移入弃牌堆
            if (CombatManager.Instance.IsInProgress && card.Pile?.Type == PileType.Play)
            {
                await CardPileCmd.Add(card, PileType.Discard);
            }
        }
    }
}
