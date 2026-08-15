using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 对象池回收防御补丁。
/// 根因：NCard 与 NGridCardHolder 都是对象池化的（NodePool，NCard prewarm 30 个），
/// 游戏原版 OnReturnedFromPool 重置了 Position/Scale/Modulate/Visible 等，
/// 但【没有重置 ZIndex】。任何代码（如随从悬停卡面对 NCard 设置 ZIndex=500）
/// 修改过 ZIndex 后若未恢复，节点回池后 ZIndex 残留，复用的卡会带着异常
/// 层级显示——正是"图层忽高忽低 / 别的牌被改了"的根因。
/// 此补丁在池对象被取出复用前强制重置 ZIndex，兜底所有污染来源。
/// </summary>
public static class PoolCleanupPatches
{
    /// <summary>
    /// NCard 复用前重置 ZIndex（游戏原版 OnReturnedFromPool 漏掉 ZIndex）
    /// </summary>
    [HarmonyPatch(typeof(NCard), nameof(NCard.OnReturnedFromPool))]
    private static class NCardPoolCleanupPatch
    {
        private static void Postfix(NCard __instance)
        {
            __instance.ZIndex = 0;
        }
    }

    /// <summary>
    /// NGridCardHolder 复用前重置 ZIndex（奖励/卡组/升级/发现界面的卡 holder，
    /// 原版 OnReturnedFromPool 同样漏掉 ZIndex）
    /// </summary>
    [HarmonyPatch(typeof(NGridCardHolder), nameof(NGridCardHolder.OnReturnedFromPool))]
    private static class NGridCardHolderPoolCleanupPatch
    {
        private static void Postfix(NGridCardHolder __instance)
        {
            __instance.ZIndex = 0;
        }
    }
}
