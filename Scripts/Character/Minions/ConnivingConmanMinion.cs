using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 蓄谋诈骗犯 (Conniving Conman) - 吉安娜专属随从。
/// 属性：攻击 4，生命 4。
/// 战吼：再次使用你使用过的上一张卡牌（重放最近施放的一张攻击/技能牌，
/// 恢复其升级级别与本局衍生状态；单目标牌随机选合法目标）。
/// </summary>
[RegisterMonster]
public sealed class ConnivingConmanMinion : JainaMinionBase
{
    /// <summary>
    /// 战斗视觉：蓄谋诈骗犯卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/conniving_conman.png";

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    /// <summary>
    /// 战吼：再次使用你使用过的上一张卡牌。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        // 只重放"我"（打出诈骗犯的玩家）施放的上一张牌——按玩家区分，联机不误用队友的
        if (!rec.LastPlayedCardByPlayer.TryGetValue(owner.NetId, out var last) ||
            last is not { } played)
        {
            return;
        }
        var (type, upgradeLevel, isGenerated) = played;

        // 按记录创建上一张卡的副本（恢复升级级别）
        var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, owner, type, upgradeLevel);
        if (card == null)
        {
            return;
        }
        if (isGenerated)
        {
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
        }

        // 单目标牌：从场上所有活物中随机选一个合法目标
        Creature? target = null;
        if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyPlayer ||
            card.TargetType == TargetType.AnyAlly ||
            (CustomTargetTypeManager.TryGetCustomTargetType(card.TargetType, out var customType) &&
             customType.IsSingleTarget))
        {
            var pool = combatState.Creatures
                .Where(c => c != null && c.IsAlive && card.IsValidTarget(c))
                .ToList();
            target = pool.Count > 0 ? owner.RunState.Rng.CombatTargets.NextItem(pool) : null;
            if (target == null)
            {
                return;
            }
        }
        // 重放的牌添加"消耗"：打出后进入消耗堆（不再进入弃牌堆，避免被反复重放）
        card.AddKeyword(CardKeyword.Exhaust);
        // 标记自动打出（不计入"手打"计数，避免重放自身膨胀）
        jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(card);
        await CardCmd.AutoPlay(choiceContext, card, target);
    }
}
