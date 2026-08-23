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
    /// 玩家回合开始前：清理玩家侧已死亡且仍挂接战斗状态的 Jaina 随从
    /// （含 CombatState 已 null 但仍残留在 creature 列表的——RemoveCreature 幂等:
    /// 没挂接的随从此摘除自身引用/从列表移除;已 null 的在下次 start 前被此处移除）。
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Owner == null || Owner.Side != side || combatState == null)
        {
            return;
        }
        // 玩家侧 creatures 快照（含主身/随从/地标）;只处理死亡且属于玩家随从的
        foreach (var creature in combatState.Creatures
                     .Where(c => c != null && !c.IsAlive && !c.IsPlayer &&
                                 c.Side == side && c.PetOwner != null &&
                                 c.Monster is jaina.Scripts.Character.Minions.JainaMinionBase)
                     .ToList())
        {
            try
            {
                if (creature.CombatState == combatState)
                {
                    combatState.RemoveCreature(creature);
                }
                else if (creature.CombatState == null && creature.Monster is { } m)
                {
                    // CombatState 已被 detach 但仍残留引用:无需再移除(不在列表),
                    // 仅记录(诊断用)
                    MegaCrit.Sts2.Core.Logging.Log.Warn(
                        $"[JainaDiag] residue pet {m.GetType().Name} already detached but lingering");
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
