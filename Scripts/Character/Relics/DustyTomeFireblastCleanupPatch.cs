using System;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using jaina.Scripts.Character.Cards;

namespace jaina.Scripts.Character.Relics;

/// <summary>
/// 修复：尘封之书（Dusty Tome）拿到"二级火焰冲击"时，牌库中的原始火焰冲击没有被移除——
/// 英雄技能唯一：超越卡（ArchaicTooth 超越/尘封之书）替代原始英雄技能，原始卡必须从牌库清除，
/// 否则牌库并存两张英雄技能卡（原始火焰冲击仍可能被发牌抽到/手动打出）。
///
/// 场景：尘封之书（[RegisterDustyTomeCard] 注册二级火焰冲击为 Jaina 先古候选）获得时
/// （原版 DustyTome.AfterObtained：创建 AncientCard（升级形态）加入牌库），
/// 原始"火焰冲击"卡仍留在牌库——原版对 starter/transcendence 卡无清理逻辑
/// （ArchaicTooth 的 Transform 会替换，DustyTome 只是 Add）。
///
/// 修复：AfterObtained Prefix 拦截——当 AncientCard 为二级火焰冲击时，从牌库移除所有火焰冲击。
/// <b>Prefix 必须为同步 void</b>（Harmony 前缀不支持 async——曾因此 PatchAll 失败初始化）。
/// 移除走与 CardPileCmd.RemoveFromDeck 核心一致的同步路径（记录移除历史 +
/// RemoveFromCurrentPile + RemoveFromState；跳过 async 动画/预览——获得遗物流程中无碍）。
/// 火焰冲击/Eternal：前置剥离 Eternal（同古老牙齿超越补丁，"不可移除"语义的其它拦截不受影响）。
/// </summary>
[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.AfterObtained))]
public static class DustyTomeFireblastCleanupPatch
{
    private static void Prefix(DustyTome __instance)
    {
        try
        {
            var owner = __instance.Owner;
            if (owner?.Deck == null || __instance.AncientCard == null)
            {
                return;
            }
            // 只处理"超越类 Ancient 卡"：获得二级火焰冲击时移除牌库中的火焰冲击
            if (__instance.AncientCard != ModelDb.GetId(typeof(FireblastAncient)))
            {
                return;
            }
            var toRemove = owner.Deck.Cards.Where(c => c is Fireblast).ToList();
            if (toRemove.Count == 0)
            {
                return;
            }
            // 临时剥离 Eternal（同 ArchaicToothTranscendencePatch；被移除卡不再恢复）
            foreach (var card in toRemove)
            {
                if (card.Keywords.Contains(CardKeyword.Eternal))
                {
                    card.RemoveKeyword(CardKeyword.Eternal);
                }
            }
            // 同步移除（CardPileCmd.RemoveFromDeck 核心：移除历史记录 + 移出牌堆 + 移出状态）
            foreach (var card in toRemove)
            {
                card.Owner.RunState.CurrentMapPointHistoryEntry?
                    .GetEntry(card.Owner.NetId).CardsRemoved.Add(card.ToSerializable());
                card.RemoveFromCurrentPile();
                card.RemoveFromState();
            }
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[JainaHeroPower] DustyTome removed {toRemove.Count} Fireblast from deck (ancient=FireblastAncient)");
        }
        catch (Exception ex)
        {
            // 移除失败不阻断尘封之书主流程（原版继续获得二级）
            MegaCrit.Sts2.Core.Logging.Log.Warn(
                $"[JainaHeroPower] DustyTome Fireblast cleanup failed: {ex.Message}");
        }
    }
}
