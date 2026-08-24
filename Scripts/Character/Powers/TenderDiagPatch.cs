using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 柔嫩（Tender）诊断日志：定位"柔嫩9层却只减1点敏捷/没减力量"——
/// 每张牌打出后（TenderPower.AfterCardPlayed 完成后）记录主人当前
/// 力量/敏捷层数；回合结束（AfterSideTurnEnd 完成后）记录恢复后的层数。
/// 观察：每次打出后力量/敏捷应逐张 -1；若一直为 0 则说明施加被某处清零。
/// </summary>
public static class TenderDiagPatch
{
    private static string Stats(PowerModel? owner)
    {
        var c = owner?.Owner;
        if (c == null)
        {
            return "owner=null";
        }
        var str = c.GetPower<StrengthPower>()?.Amount;
        var dex = c.GetPower<DexterityPower>()?.Amount;
        return $"owner={c.Name}(side={c.Side}) str={str?.ToString() ?? "none"} dex={dex?.ToString() ?? "none"}";
    }

    [HarmonyPatch(typeof(TenderPower), nameof(TenderPower.AfterCardPlayed))]
    private static class AfterCardPlayedDiag
    {
        private static async Task Postfix(TenderPower __instance)
        {
            await Task.Yield();
            MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaTender] AfterCardPlayed: {Stats(__instance)}");
        }
    }

    [HarmonyPatch(typeof(TenderPower), nameof(TenderPower.AfterSideTurnEnd))]
    private static class AfterSideTurnEndDiag
    {
        private static async Task Postfix(TenderPower __instance)
        {
            await Task.Yield();
            MegaCrit.Sts2.Core.Logging.Log.Info($"[JainaTender] AfterSideTurnEnd: {Stats(__instance)}");
        }
    }
}
