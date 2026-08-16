using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Minions;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 随从独立承伤补丁。
/// 0.111.1 中 `CreatureCmd.Damage` 对任何有 PetOwner 的目标（宠物/随从）都会
/// 先把伤害用主人护甲格挡（CreatureCmd.cs L286-287：
/// `Creature creature = originalTarget.PetOwner?.Creature ?? originalTarget`），
/// 导致"法术/攻击指向随从"时伤害被吉安娜护甲吸收（炉石语义下随从应独立承伤）。
/// 通过 Transpiler 把该处的 `get_PetOwner` 替换为 <see cref="GetDamageBlockPetOwner"/>：
/// Jaina 随从（MinionModel）返回 null（= 随从自己承伤），其余宠物（Osty 等）保持原行为。
/// 注意：CreatureCmd.Damage 是 async 方法，实际 IL 在编译器生成的状态机
/// <c>&lt;Damage&gt;d__*::MoveNext</c> 中，因此用 TargetMethod() 定位状态机
/// （按 <c>&lt;originalTarget&gt;5__8</c> 字段探测），不能直接 patch 声明方法
/// （声明方法体只有 AsyncTaskMethodBuilder 包装，Transpiler 匹配不到 get_PetOwner）。
/// </summary>
[HarmonyPatch]
public static class PetDamageRedirectPatch
{
    private static MethodBase TargetMethod()
    {
        // 定位 CreatureCmd.Damage 的状态机：含 <originalTarget>5__8 字段的 <Damage>d__* 嵌套类
        foreach (var type in typeof(CreatureCmd).GetNestedTypes(BindingFlags.NonPublic))
        {
            if (!type.Name.StartsWith("<Damage>d__", System.StringComparison.Ordinal))
            {
                continue;
            }
            if (type.GetField("<originalTarget>5__8", BindingFlags.Instance | BindingFlags.NonPublic) != null)
            {
                return type.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }
        return null;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instr in instructions)
        {
            if (instr.opcode == OpCodes.Callvirt && instr.operand is MethodInfo mi &&
                mi.Name == "get_PetOwner" && mi.DeclaringType == typeof(Creature))
            {
                // originalTarget 已在栈上（get_PetOwner 的实例），把 get_PetOwner 换成静态方法
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(PetDamageRedirectPatch), nameof(GetDamageBlockPetOwner)));
            }
            else
            {
                yield return instr;
            }
        }
    }

    /// <summary>
    /// 护甲结算用的"归属主人"：Jaina 随从返回 null（独立承伤，不走主人护甲），
    /// 其余宠物（Osty 等）保持原行为（主人护甲格挡）。
    /// </summary>
    public static Player? GetDamageBlockPetOwner(Creature target)
    {
        if (target != null && target.Monster is JainaMinionBase)
        {
            return null;
        }
        return target?.PetOwner;
    }
}
