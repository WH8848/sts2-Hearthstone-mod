using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 修复联机"队友回合结束按键消失"：
/// 主机死亡后的玩家回合切换中，客机端 NEndTurnButton 可能停留在
/// Hidden/Disabled 状态（原版 OnTurnStarted 依赖的恢复路径在死亡玩家
/// 被自动 SetReady 的时序下可能被跳过），导致存活的客机玩家看不到
/// 回合结束按钮。
///
/// 防御性修复：玩家回合开始（OnTurnStarted）后，若本地玩家存活、
/// 未就绪结束回合且当前处于 PlayPhase，强制把按钮恢复到 Enabled
/// （AnimIn + Enable），与正常回合行为一致。纯本地 UI 修复，联机安全。
/// </summary>
[HarmonyPatch(typeof(NEndTurnButton), "OnTurnStarted")]
public static class EndTurnButtonRestorePatch
{
    private static readonly FieldInfo _stateField = AccessTools.Field(typeof(NEndTurnButton), "_state");
    private static readonly MethodInfo _animIn = AccessTools.Method(typeof(NEndTurnButton), "AnimIn");

    public static void Postfix(NEndTurnButton __instance, CombatState state)
    {
        try
        {
            // 仅玩家侧回合开始需要恢复按钮
            if (state == null || state.CurrentSide != CombatSide.Player)
            {
                return;
            }
            if (!CombatManager.Instance.IsInProgress)
            {
                return;
            }
            // 本地玩家：存活才需要回合结束按钮（死亡玩家按钮本就该隐藏）
            Player me = LocalContext.GetMe(state);
            if (me == null || !me.Creature.IsAlive)
            {
                return;
            }
            // 本地玩家已就绪结束回合：按钮不应恢复（等待队友或已结束）
            if (CombatManager.Instance.IsPlayerReadyToEndTurn(me))
            {
                return;
            }
            // 按钮当前是否 Enabled（枚举顺序：Enabled=0, Disabled=1, Hidden=2）
            int currentState = (int)(_stateField?.GetValue(__instance) ?? 2);
            if (currentState == 0)
            {
                return;
            }
            // 兜底恢复：飞出屏幕的按钮飞回 + 启用（原版 SetState(Enabled) 内部
            // 也是 AnimIn + RefreshEnabled→Enable）
            _animIn?.Invoke(__instance, null);
            __instance.Enable();
            // 同步私有状态字段，保持与 UI 一致（Enable 不改 _state）
            if (_stateField != null)
            {
                _stateField.SetValue(__instance, 0);
            }
        }
        catch
        {
            // 修复失败不影响战斗
        }
    }
}
