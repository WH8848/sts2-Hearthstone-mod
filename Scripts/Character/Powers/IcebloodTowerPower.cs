using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 冰血哨塔：在你的回合结束时，随机从你的抽牌堆中施放另一个法术。
/// 挂在吉安娜玩家身上（可见）。
/// </summary>
[RegisterPower]
public sealed class IcebloodTowerPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_iceblood_tower_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 玩家回合结束：随机从抽牌堆中选一个法术牌，免费自动施放（单目标随机选合法目标）。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner == null || Owner.Side != side)
        {
            return;
        }
        var player = Owner.Player;
        if (player == null || player.PlayerCombatState == null)
        {
            return;
        }
        var combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 抽牌堆中的法术牌（攻击/技能牌，不含英雄技能卡）
        var spells = player.PlayerCombatState.DrawPile.Cards
            .Where(c => c != null &&
                        (c.Type == CardType.Attack || c.Type == CardType.Skill) &&
                        !HeroPowerHandHelper.IsHeroPowerCard(c))
            .ToList();
        if (spells.Count == 0)
        {
            return;
        }
        var rng = player.RunState.Rng.CombatTargets;
        var card = rng.NextItem(spells);
        if (card == null)
        {
            return;
        }

        // 单目标牌：从场上所有活物中随机选一个合法目标（联机可打队友）
        Creature? target = null;
        if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyPlayer ||
            card.TargetType == TargetType.AnyAlly ||
            (CustomTargetTypeManager.TryGetCustomTargetType(card.TargetType, out var customType) &&
             customType.IsSingleTarget))
        {
            var pool = combatState.Creatures
                .Where(c => c != null && c.IsAlive && card.IsValidTarget(c))
                .ToList();
            target = pool.Count > 0 ? rng.NextItem(pool) : null;
            if (target == null)
            {
                return;
            }
        }
        await CardCmd.AutoPlay(choiceContext, card, target);
    }
}
