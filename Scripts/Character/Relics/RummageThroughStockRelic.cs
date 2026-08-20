using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Relics;

/// <summary>
/// 乱翻库存 (Rummage Through Stock) - 吉安娜罕见遗物。
/// 你每在你的回合发现1张牌，就对1名随机敌人造成3点伤害。
/// 触发点：DiscoverTracker.OnCardAddedToHand（所有发现路径统一入口，
/// 含原版/mod 发现；按玩家区分，联机各自触发；随机释放触发的发现不触发）。
/// </summary>
[RegisterRelic(typeof(JainaRelicPool))]
public sealed class RummageThroughStockRelic : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    /// <summary>
    /// 遗物图标：小图 85x85（程序绘制占位，卡图待 wiki 原画）
    /// </summary>
    public override string? CustomIconPath => "res://assets/relic_icons/rummage_through_stock_icon.png";

    /// <summary>
    /// 遗物轮廓图标：85x85
    /// </summary>
    public override string? CustomIconOutlinePath => "res://assets/relic_icons/rummage_through_stock_outline.png";

    /// <summary>
    /// 遗物大图：256x256
    /// </summary>
    public override string? CustomBigIconPath => "res://assets/relic_icons/rummage_through_stock_big.png";

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 玩家每发现 1 张牌触发：若其拥有本遗物，对 1 名随机可命中敌人造成 3 点伤害。
    /// 在发现入手的前缀钩子（同步）中调用——伤害用 fire-and-forget 异步执行
    /// （Throwing 上下文 + CombatTargets RNG，联机两端确定性一致）。
    /// </summary>
    public static void TriggerOnDiscover(Player player)
    {
        if (player == null || player.Creature?.CombatState == null)
        {
            return;
        }
        if (player.GetRelic<RummageThroughStockRelic>() == null)
        {
            return;
        }
        _ = TaskHelper.RunSafely(DealDamageAsync(player));
    }

    private static async Task DealDamageAsync(Player player)
    {
        var combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var enemies = combatState.GetOpponentsOf(player.Creature)
            .Where(e => e != null && e.IsAlive && e.IsHittable)
            .ToList();
        if (enemies.Count == 0)
        {
            return;
        }
        var target = player.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target == null)
        {
            return;
        }
        // 3 点伤害（Move 标记：吃力量/易伤等修正；施害者为玩家角色）
        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            [target],
            3m,
            ValueProp.Move,
            player.Creature,
            null,
            null);
    }
}
