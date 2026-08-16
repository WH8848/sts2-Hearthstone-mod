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
/// </summary>
[HarmonyPatch]
public static class JainaDeathrattleDamagePatch
{
    private static MethodBase TargetMethod()
    {
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
        var isResolvingGetter =
            AccessTools.PropertyGetter(typeof(JainaDeathrattleHelper), nameof(JainaDeathrattleHelper.IsResolvingDeathrattle));
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
                //   call get_IsResolvingDeathrattle
                //   brtrue L_ok             （亡语结算中 → 正常，放行死 dealer）
                codes.InsertRange(i + 2, new[]
                {
                    new CodeInstruction(OpCodes.Call, isResolvingGetter),
                    new CodeInstruction(OpCodes.Brtrue, br.operand)
                });
                break;
            }
        }
        return codes;
    }
}
