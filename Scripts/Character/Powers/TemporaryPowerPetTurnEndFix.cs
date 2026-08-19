using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 临时属性（力量/专注/敏捷）随主人回合结束消失——修复随从身上的临时属性不消失：
/// 原版 TemporaryStrengthPower/TemporaryFocusPower/TemporaryDexterityPower 的
/// AfterSideTurnEnd 只在 participants（仅玩家角色 Creature，不含随从 Pet）包含
/// 自己时才移除——随从（Pet）身上的临时属性（如王之凝视的力量下降）永不消失。
/// 扩展：随从的主人的回合结束时，随从身上的临时属性同样移除并恢复对应属性。
/// 与项目既有模式一致（fire-and-forget 移除，无等待，竞态窗口极小）。
/// </summary>
internal static class TemporaryPowerPetTurnEndHelper
{
    /// <summary>该临时属性是正向（+力量）还是负向（-力量，如王之凝视）</summary>
    public static bool IsPositive(PowerModel power)
    {
        try
        {
            return Traverse.Create(power).Property("IsPositive").GetValue<bool>();
        }
        catch
        {
            return true;
        }
    }
}

/// <summary>临时力量：主人回合结束时随从身上的临时力量同样移除并恢复。</summary>
[HarmonyPatch(typeof(TemporaryStrengthPower), "AfterSideTurnEnd")]
public static class TemporaryStrengthPetTurnEndFix
{
    public static bool Prefix(TemporaryStrengthPower __instance, PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        var owner = __instance.Owner;
        if (owner == null || owner.IsDead || participants.Contains(owner))
        {
            return true; // 原逻辑（含主人）
        }
        // 随从：主人的回合结束 → 临时力量同样消失
        if (owner.PetOwner != null && participants.Contains(owner.PetOwner))
        {
            var amount = __instance.Amount;
            bool positive = TemporaryPowerPetTurnEndHelper.IsPositive(__instance);
            _ = RemoveAndRestoreAsync(__instance, choiceContext, owner, positive ? -amount : amount);
            return false;
        }
        return true;
    }

    private static async Task RemoveAndRestoreAsync(TemporaryStrengthPower power,
        PlayerChoiceContext choiceContext, Creature owner, decimal restore)
    {
        try
        {
            await PowerCmd.Remove(power);
            await PowerCmd.Apply<StrengthPower>(choiceContext, owner, restore, owner, null);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] temporary strength pet turn-end restore failed: {ex}");
        }
    }
}

/// <summary>临时专注：主人回合结束时随从身上的临时专注同样移除并恢复。</summary>
[HarmonyPatch(typeof(TemporaryFocusPower), "AfterSideTurnEnd")]
public static class TemporaryFocusPetTurnEndFix
{
    public static bool Prefix(TemporaryFocusPower __instance, PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        var owner = __instance.Owner;
        if (owner == null || owner.IsDead || participants.Contains(owner))
        {
            return true;
        }
        if (owner.PetOwner != null && participants.Contains(owner.PetOwner))
        {
            var amount = __instance.Amount;
            bool positive = TemporaryPowerPetTurnEndHelper.IsPositive(__instance);
            _ = RemoveAndRestoreAsync(__instance, choiceContext, owner, positive ? -amount : amount);
            return false;
        }
        return true;
    }

    private static async Task RemoveAndRestoreAsync(TemporaryFocusPower power,
        PlayerChoiceContext choiceContext, Creature owner, decimal restore)
    {
        try
        {
            await PowerCmd.Remove(power);
            await PowerCmd.Apply<FocusPower>(choiceContext, owner, restore, owner, null);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] temporary focus pet turn-end restore failed: {ex}");
        }
    }
}

/// <summary>临时敏捷：主人回合结束时随从身上的临时敏捷同样移除并恢复。</summary>
[HarmonyPatch(typeof(TemporaryDexterityPower), "AfterSideTurnEnd")]
public static class TemporaryDexterityPetTurnEndFix
{
    public static bool Prefix(TemporaryDexterityPower __instance, PlayerChoiceContext choiceContext,
        CombatSide side, IEnumerable<Creature> participants)
    {
        var owner = __instance.Owner;
        if (owner == null || owner.IsDead || participants.Contains(owner))
        {
            return true;
        }
        if (owner.PetOwner != null && participants.Contains(owner.PetOwner))
        {
            var amount = __instance.Amount;
            bool positive = TemporaryPowerPetTurnEndHelper.IsPositive(__instance);
            _ = RemoveAndRestoreAsync(__instance, choiceContext, owner, positive ? -amount : amount);
            return false;
        }
        return true;
    }

    private static async Task RemoveAndRestoreAsync(TemporaryDexterityPower power,
        PlayerChoiceContext choiceContext, Creature owner, decimal restore)
    {
        try
        {
            await PowerCmd.Remove(power);
            await PowerCmd.Apply<DexterityPower>(choiceContext, owner, restore, owner, null);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] temporary dexterity pet turn-end restore failed: {ex}");
        }
    }
}
