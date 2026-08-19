using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 源生之石光环：每当你发现一张牌后，会自动使用其余选项（免费自动打出，随机目标）
/// 并失去 1 点耐久度。耐久度（Amount，初始 8）为 0 时能力消失。
/// 挂在玩家身上，打出源生之石时施加。可见（能力图标显示剩余耐久）。
/// </summary>
[RegisterPower]
public sealed class ForbiddenStonePower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_forbidden_stone_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    private long _lastSeq;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => true;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner?.Player;
        if (player == null || cardPlay.Card.Owner != player)
        {
            return;
        }
        // 自动打出（AutoPlay：匣中古神/惊奇卡牌/戏法图腾/大法师的符文/罗曼斯/重放等
        // 随机释放）的卡不触发源生之石：其触发的发现不响应（DiscoverTracker.IsAuto），
        // 且自动使用其余选项会再次打出卡 → 再次触发本钩子 → 若发现判定失效则无限递归
        // （曾导致打出源生之石后卡牌卡在空中）。只响应玩家手打的卡。
        if (cardPlay.IsAutoPlay)
        {
            return;
        }
        await ConsumePendingDiscover(choiceContext, player);
    }

    public override async Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        var player = Owner?.Player;
        if (player == null || potion.Owner != player)
        {
            return;
        }
        // 药水使用钩子无 PlayerChoiceContext：随机目标无玩家交互，用 Throwing 上下文
        await ConsumePendingDiscover(new ThrowingPlayerChoiceContext(), player);
    }

    private async Task ConsumePendingDiscover(PlayerChoiceContext choiceContext, Player player)
    {
        // 循环消费所有未处理发现（广阔智慧一次发现两张 → 触发两次，每次自动使用其余选项并失去1点耐久）
        while (DiscoverTracker.TryGetPending(player, _lastSeq) is { } pending)
        {
            _lastSeq = pending.Seq;

            // 随机释放（自动打出）触发的发现不触发源生之石：跳过（只推进游标不消费）
            if (pending.IsAuto)
            {
                continue;
            }

            var combatState = player.Creature.CombatState;
            // 自动使用其余选项：免费自动打出（随机目标），与罗曼斯重放同一语义
            foreach (var other in pending.Others)
            {
                Creature? target = null;
                if (other.TargetType == TargetType.AnyEnemy || other.TargetType == TargetType.AnyPlayer ||
                    other.TargetType == TargetType.AnyAlly ||
                    (CustomTargetTypeManager.TryGetCustomTargetType(other.TargetType, out var customType) &&
                     customType.IsSingleTarget))
                {
                    var pool = combatState.Creatures
                        .Where(c => c != null && c.IsAlive && other.IsValidTarget(c))
                        .ToList();
                    target = pool.Count > 0 ? player.RunState.Rng.CombatTargets.NextItem(pool) : null;
                    if (target == null)
                    {
                        continue;
                    }
                }
                await CardCmd.AutoPlay(choiceContext, other, target);
            }

            // 失去 1 点耐久度；耐久为 0 时能力消失
            await PowerCmd.Decrement(this);
            if (Amount <= 0)
            {
                await PowerCmd.Remove(this);
                return;
            }
        }
    }
}
