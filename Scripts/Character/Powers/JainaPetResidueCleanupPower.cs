using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 死亡随从残留兜底清理（隐藏 Power,挂在本局对战玩家身上）。
/// 实测（2026-08-23 多人,2 端同卡死）：已死 Jaina 随从（CombatState 已被置 null）
/// 仍留在 CombatState 的玩家侧列表 → 回合开始 StartTurn → AfterTurnStart →
/// ClearBlock → ShouldClearBlock(null) → NRE → "turn loop died" 战斗卡死
/// （"回合结束无法开始下一个回合"）。
/// 即使 <see cref="jaina.Scripts.Character.Minions.JainaMinionBase.AfterDeath"/>
/// 的主动移除因时序未生效,本 Power 在每个玩家回合开始前也会把
/// 玩家侧已死的 Jaina 随从从 CombatState 摘除（幂等,try/catch 保护）。
/// </summary>
[RegisterPower]
public sealed class JainaPetResidueCleanupPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 隐藏：防御性机制,不显示图标
    /// </summary>
    protected override bool IsVisibleInternal => false;

    /// <summary>
    /// <b>任意侧</b>回合开始前：清理玩家侧已死亡且仍挂接战斗状态的 Jaina 随从。
    /// 重要：不能只在自己回合清理——敌方回合开始的 AfterTurnStart→ClearBlock
    /// 同样会遍历玩家侧死随从（ShouldClearBlock(null) NRE → "turn loop died"
    /// 战斗卡死,后续回合随从行动点不再恢复——实测 2026-08-24 SPINY_TOAD 战斗 #11,
    /// 死于敌人回合的星术师索兰莉安正是 NRE 来源）。
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner == null || combatState == null)
        {
            return;
        }
        // 玩家侧 creatures 快照（含主身/随从/地标）;只处理死亡且属于玩家随从的
        foreach (var creature in combatState.Creatures
                     .Where(c => c != null && !c.IsAlive && !c.IsPlayer &&
                                 c.Side == Owner.Side && c.PetOwner != null &&
                                 c.Monster is jaina.Scripts.Character.Minions.JainaMinionBase)
                     .ToList())
        {
            try
            {
                if (creature.CombatState == combatState)
                {
                    combatState.RemoveCreature(creature);
                    MegaCrit.Sts2.Core.Logging.Log.Warn(
                        $"[JainaDiag] residue pet {creature.Monster?.GetType().Name} removed at side {side} start");
                }
                else if (creature.CombatState == null && creature.Monster is { } m)
                {
                    // CombatState 已 detach 但仍残留引用（"残留尸体"）：
                    // 原版 CombatState.RemoveCreature 对 CombatState==null 直接早退,
                    // 尸体将永久留在玩家侧列表——主机端在 AfterDeath 时移除成功、
                    // 客户端 CombatState 已先被置 null 移除失败 → 两端 creature 列表
                    // 不一致 → 下个回合结束 checksum 分歧(StateDivergence 断联,
                    // 实测:莫扎奇尸体 客户端[4]0/8 残留 vs 主机无 — checksum #93)。
                    // 这里直接从私列表强制摘除(两端确定性执行,彻底对称)。
                    ForceRemoveLingering(combatState, creature);
                    MegaCrit.Sts2.Core.Logging.Log.Warn(
                        $"[JainaDiag] residue pet {m.GetType().Name} lingering -> force removed");
                }
            }
            catch (System.Exception ex)
            {
                MegaCrit.Sts2.Core.Logging.Log.Warn(
                    $"[JainaDiag] residue cleanup failed on {creature.Monster?.GetType().Name}: {ex}");
            }
        }
    }

    /// <summary>
    /// 从 CombatState 私列表(<c>_allies/_enemies</c>)强制摘除"残留尸体"
    /// (CombatState 已被置 null 但仍留在玩家侧/敌方列表中的已死生物)。
    /// 原版 <see cref="CombatState.RemoveCreature"/> 对 CombatState==null 直接早退,
    /// 无法清理该状态;这里是联机确定性修复(caller 均为两端的确定性钩子,
    /// 两端各自执行等价摘除,结果一致)。反射访问私列表,失败静默不阻断战斗。
    /// </summary>
    internal static void ForceRemoveLingering(ICombatState combatState, Creature creature)
    {
        try
        {
            if (combatState is not MegaCrit.Sts2.Core.Combat.CombatState cs)
            {
                return;
            }
            bool removed = false;
            foreach (var fieldName in new[] { "_allies", "_enemies" })
            {
                var field = HarmonyLib.AccessTools.Field(typeof(MegaCrit.Sts2.Core.Combat.CombatState), fieldName);
                if (field?.GetValue(cs) is System.Collections.Generic.List<Creature> list)
                {
                    if (list.Remove(creature))
                    {
                        removed = true;
                    }
                }
            }
            if (removed)
            {
                // CreaturesChanged 是事件,外部只能经反射触发(失败静默——纯 UI 通知)
                var evtField = HarmonyLib.AccessTools.Field(
                    typeof(MegaCrit.Sts2.Core.Combat.CombatState), "CreaturesChanged");
                if (evtField?.GetValue(cs) is System.Action<MegaCrit.Sts2.Core.Combat.CombatState> action)
                {
                    action(cs);
                }
            }
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[JainaDiag] force remove lingering failed: {ex}");
        }
    }

    /// <summary>
    /// 施加本兜底(幂等,挂玩家主身)——由初始遗物战斗开始时调用
    /// </summary>
    public static async Task EnsureAppliedAsync(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player?.Creature?.CombatState == null)
        {
            return;
        }
        if (player.Creature.GetPower<JainaPetResidueCleanupPower>() != null)
        {
            return;
        }
        await PowerCmd.Apply<JainaPetResidueCleanupPower>(
            choiceContext, player.Creature, 1m, player.Creature, null);
    }
}
