using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 炉石亡语语义辅助：亡语效果正常结算，来源是死掉的随从本身。
/// 引擎限制：CreatureCmd.Damage 对"已死亡的 dealer"直接返回空结果（不造成伤害）。
/// 本辅助在亡语结算期间设置标记，patch 让该检查放行——亡语伤害来源保持为死掉的随从。
/// 其余场景（非亡语结算）保持原版行为。
/// </summary>
public static class JainaDeathrattleHelper
{
    /// <summary>
    /// 是否正在结算亡语效果（亡语伤害放行死 dealer 的标记）
    /// </summary>
    public static bool IsResolvingDeathrattle;
}

/// <summary>
/// CreatureCmd.Damage 核心重载：把"dealer 已死 → 返回空结果"改为
/// "dealer 已死 且 不在亡语结算中 → 返回空结果"。
/// 亡语结算期间（IsResolvingDeathrattle=true）死随从照常造成伤害（炉石语义）。
/// 注意：Damage 是 async 方法，实际逻辑在 <Damage>d__*::MoveNext 状态机中——
/// patch wrapper 方法体为空（无 IsDead 检查），必须 patch 状态机。
/// </summary>
[HarmonyPatch]
public static class JainaDeathrattleDamagePatch
{
    private static MethodBase TargetMethod()
    {
        // 定位 Damage 核心重载的状态机：<Damage>d__* 中含 <results> 字段的嵌套类
        // （核心重载有局部变量 result/List<DamageResult>；其它重载状态机无此字段。
        //  注意：新版编译器把 async 参数存为 <>8__N 槽，不能用参数名探测。）
        foreach (var type in typeof(CreatureCmd).GetNestedTypes(BindingFlags.NonPublic))
        {
            if (!type.Name.StartsWith("<Damage>d__", System.StringComparison.Ordinal))
            {
                continue;
            }
            bool hasResultsField = false;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.Name.Contains("results"))
                {
                    hasResultsField = true;
                    break;
                }
            }
            if (hasResultsField)
            {
                return type.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }
        MegaCrit.Sts2.Core.Logging.Log.Warn("[JainaDeathrattlePatch] state machine not found, falling back to wrapper");
        // Fallback：patch wrapper 方法（Transpiler 无匹配则无效，但不崩溃）
        return typeof(CreatureCmd).GetMethod(nameof(CreatureCmd.Damage),
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[]
            {
                typeof(PlayerChoiceContext),
                typeof(IEnumerable<Creature>),
                typeof(decimal),
                typeof(ValueProp),
                typeof(Creature),
                typeof(CardModel),
                typeof(CardPlay)
            },
            null);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var isDeadGetter = AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.IsDead));
        // IsResolvingDeathrattle 是静态字段（不是属性），用 Ldsfld 加载
        var isResolvingField =
            AccessTools.Field(typeof(JainaDeathrattleHelper), nameof(JainaDeathrattleHelper.IsResolvingDeathrattle));
        for (int i = 0; i < codes.Count; i++)
        {
            // 定位：callvirt Creature.get_IsDead() 后跟 brfalse/brfalse.s（!IsDead → 正常伤害流程）
            if (codes[i].opcode == OpCodes.Callvirt && Equals(codes[i].operand, isDeadGetter) &&
                i + 1 < codes.Count &&
                (codes[i + 1].opcode == OpCodes.Brfalse || codes[i + 1].opcode == OpCodes.Brfalse_S))
            {
                var br = codes[i + 1];
                // 替换为：
                //   brfalse L_ok            （!IsDead → 正常）
                //   ldsfld IsResolvingDeathrattle
                //   brtrue L_ok             （亡语结算中 → 正常，放行死 dealer）
                codes.InsertRange(i + 2, new[]
                {
                    new CodeInstruction(OpCodes.Ldsfld, isResolvingField),
                    new CodeInstruction(OpCodes.Brtrue, br.operand)
                });
                break;
            }
        }
        return codes;
    }
}
