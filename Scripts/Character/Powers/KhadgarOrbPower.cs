using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;
using jaina.Scripts.Character.Weapons;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 魔法智慧之球效果：在你的回合结束时，随机施放一个有用的法师法术
/// （火球术/寒冰箭/烈焰风暴/暴风雪/法术反制/寒冰护盾），随后失去 1 点耐久度。
/// 耐久度为 0 时武器能力消失（球效果一并移除）。
/// 挂在玩家身上，装备魔法智慧之球时施加。可见（能力图标）。
/// </summary>
[RegisterPower]
public sealed class KhadgarOrbPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_khadgar_orb_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 有用的法师法术池（均为吉安娜已注册的法术牌；烈焰风暴/暴风雪/法术反制
    /// 分别为火焰护盾/冰冷吐息/异议的升级形态）
    /// </summary>
    private static readonly (System.Type Type, int UpgradeLevel)[] UsefulMageSpells =
    [
        (typeof(Fireball), 0),
        (typeof(Frostbolt), 0),
        (typeof(FlameWard), 1),   // 烈焰风暴
        (typeof(ConeOfCold), 1),  // 暴风雪
        (typeof(Objection), 1),   // 法术反制
        (typeof(IceBarrier), 0),
    ];

    /// <summary>
    /// 玩家回合结束时：随机施放一个有用的法师法术（免费自动打出，随机目标），
    /// 然后失去 1 点耐久度（0 时武器能力消失，球效果移除）。
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Combat.CombatSide side, IEnumerable<Creature> participants)
    {
        var player = Owner?.Player;
        if (player == null || side != MegaCrit.Sts2.Core.Combat.CombatSide.Player)
        {
            return;
        }
        var weapon = Owner.Powers.OfType<JainaWeaponPower>().FirstOrDefault();
        if (weapon == null)
        {
            // 武器已消失（被顶替/耐久耗尽）：球效果一并移除
            await PowerCmd.Remove(this);
            return;
        }

        // 随机施放一个有用的法师法术
        await CastRandomMageSpell(choiceContext, player);

        // 失去 1 点耐久度；耐久为 0 时武器能力消失（球效果一并移除）
        await JainaWeaponSlot.ConsumeDurability(choiceContext, Owner, weapon);
        if (weapon.Amount <= 0)
        {
            await PowerCmd.Remove(this);
        }
    }

    /// <summary>
    /// 从有用法师法术池随机选一张，按升级级别创建实例并免费自动打出（随机目标）。
    /// </summary>
    private async Task CastRandomMageSpell(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rng = player.RunState.Rng.CombatCardSelection;
        var (type, upgradeLevel) = rng.NextItem(UsefulMageSpells);
        if (type == null)
        {
            return;
        }
        var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, player, type, upgradeLevel);
        if (card == null)
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);

        // 单目标法术：随机选合法目标（与罗曼斯重放同一语义）
        Creature? target = null;
        if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyPlayer ||
            card.TargetType == TargetType.AnyAlly ||
            (CustomTargetTypeManager.TryGetCustomTargetType(card.TargetType, out var customType) &&
             customType.IsSingleTarget))
        {
            var pool = combatState.Creatures
                .Where(c => c != null && c.IsAlive && card.IsValidTarget(c))
                .ToList();
            target = pool.Count > 0 ? player.RunState.Rng.CombatTargets.NextItem(pool) : null;
            if (target == null)
            {
                return;
            }
        }
        await CardCmd.AutoPlay(choiceContext, card, target);
    }
}
