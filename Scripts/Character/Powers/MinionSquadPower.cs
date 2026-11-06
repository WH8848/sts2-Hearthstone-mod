using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 随从军势 - 吉安娜的随从守卫能力。
/// 当吉安娜的护甲无法阻挡伤害时，按随从召唤顺序扣除随从生命值来抵挡。
/// 所有随从生命值都不足以抵挡时，剩余伤害才会扣到吉安娜。
/// </summary>
[RegisterPower]
public sealed class MinionSquadPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_minion_squad_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 幂等挂载随从军势（战斗开始牌库检测 / 召唤随从时共用）。
    /// 已有则不动。
    /// </summary>
    public static async Task EnsureAppliedAsync(PlayerChoiceContext choiceContext, Player player)
    {
        if (player?.Creature == null || player.Creature.Powers.Any(p => p is MinionSquadPower))
        {
            return;
        }
        await PowerCmd.Apply<MinionSquadPower>(choiceContext, [player.Creature], 1m, player.Creature, null);
    }

    /// <summary>
    /// 拦截未格挡伤害 - 按随从召唤顺序扣除随从 HP
    /// </summary>
    public override decimal ModifyHpLostBeforeOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 只拦截对吉安娜本体的伤害
        if (target != base.Owner)
        {
            return amount;
        }
        if (amount <= 0m)
        {
            return amount;
        }
        // 预测模拟防御：RandomForeseer（随机数预测 mod）的 CombatPredictionSimulator 会调用
        // Hook.ModifyHpLost 做"预览"，而本 Hook 会真实扣随从血（LoseHpInternal）——预测产生
        // 真实副作用：每次预测敌人伤害，随从就被真实扣一次血（表现为"敌人掉多少随从掉多少"）。
        // 预测调用直接跳过（不拦截、不扣血），真实伤害照常挡伤。
        if (IsPredictionCall(cardSource))
        {
            return amount;
        }
        // 罗曼斯重放的额外法术对自己造成的伤害不承担（直接生效）
        if (cardSource != null && RommathReplayTracker.IsMarked(cardSource))
        {
            return amount;
        }

        // 获取吉安娜的所有存活随从（生物随从）
        var player = base.Owner.Player;
        if (player?.PlayerCombatState?.Pets == null)
        {
            return amount;
        }

        // 按召唤顺序（Pets 列表顺序即召唤顺序）遍历随从（地标不参与挡刀）
        var minions = player.PlayerCombatState.Pets
            .Where(p => p != null && p.IsAlive && p.Monster is JainaMinionBase && p.Monster is not JainaLandmarkBase)
            .ToList();

        foreach (var minion in minions)
        {
            decimal absorbed = decimal.Min(amount, minion.CurrentHp);
            if (absorbed > 0m)
            {
                // 扣随从生命（走 LoseHpInternal 避免再次触发挡伤递归）
                minion.LoseHpInternal(absorbed, ValueProp.Unpowered);
                Flash();
                amount -= absorbed;
            }

            // 随从被挡伤致死：LoseHpInternal 不会触发死亡流程，
            // 必须手动走 Kill（触发死亡动画、移除战场节点、亡语），
            // 否则尸体卡图留在场上挡住其他随从。
            if (minion.IsDead)
            {
                _ = CreatureCmd.Kill(minion, force: false);
            }

            if (amount <= 0m)
            {
                break;
            }
        }

        return amount;
    }

    /// <summary>
    /// 是否为第三方预测模拟调用（RandomForeseer 的预测卡 / 预测模拟栈）。
    /// 预测调用不得产生真实副作用（扣随从血 / Kill）。
    /// </summary>
    private static bool IsPredictionCall(CardModel? cardSource)
    {
        try
        {
            if (cardSource != null &&
                cardSource.GetType().FullName?.Contains("PredictedCard", System.StringComparison.Ordinal) == true)
            {
                return true;
            }
            return new System.Diagnostics.StackTrace().ToString()
                .Contains("RandomForeseer", System.StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
