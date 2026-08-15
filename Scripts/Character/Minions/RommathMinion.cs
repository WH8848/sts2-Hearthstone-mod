using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MinionLib.Minion;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 大法师罗曼斯 (Archmage Rommath) - 吉安娜专属随从。
/// 属性：攻击 5，生命 7。
/// 战吼：再次施放你在本局对战中施放的每个牌库之外的攻击牌或技能牌（随机目标）。
/// </summary>
[RegisterMonster]
public sealed class RommathMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 7;

    public override int MaxInitialHp => 7;

    protected override string MinionVisualsPath => "res://assets/card_art/archmage_rommath.png";

    /// <summary>
    /// 战吼：重放本局玩家手打的每张牌库之外的攻击/技能牌（免费自动打出，随机目标）。
    /// 按"玩家手打的次数"重放（炉石：每次施放都重放）——打出 N 张衍生火球术就重放 N 次。
    /// 仅手牌打出时触发，随机召唤不触发。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        var combatState = Creature.CombatState;
        if (owner == null || combatState == null)
        {
            return;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        // 快照遍历，避免重放触发的新记录影响本循环
        var counts = rec.PlayerCastOutsideDeckCounts.ToList();
        foreach (var (type, count) in counts)
        {
            for (int i = 0; i < count; i++)
            {
                rec.GeneratedUpgradeLevels.TryGetValue(type, out var upgradeLevel);
                var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                    combatState, owner, type, upgradeLevel);
                if (card == null)
                {
                    continue;
                }

                // 需要目标时（单目标卡：原生 AnyEnemy/AnyPlayer 或自定义单目标类型），
                // 按卡的目标校验从场上活物中随机选一个合法目标
                // （涵盖敌人与己方随从——如 EnemyOrOwnMinion 这类自定义目标类型）。
                Creature? target = null;
                if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyPlayer ||
                    card.TargetType == TargetType.AnyAlly ||
                    (MinionLib.Targeting.CustomTargetTypeManager.TryGetCustomTargetType(card.TargetType, out var customType) &&
                     customType.IsSingleTarget))
                {
                    var pool = combatState.Creatures
                        .Where(c => c != null && c.IsAlive && card.IsValidTarget(c))
                        .ToList();
                    target = pool.Count > 0 ? owner.RunState.Rng.CombatTargets.NextItem(pool) : null;
                    if (target == null)
                    {
                        continue;
                    }
                }
                // AutoPlay：免费自动打出（不消耗能量），随机目标语义已由上方处理。
                // 标记为"罗曼斯重放卡"：其对自己造成的伤害不触发随从军势挡伤；
                // 同时标记为牌库之外（打开时空之门等计数"牌库外的法术"施放）。
                jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(card);
                jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
                await CardCmd.AutoPlay(choiceContext, card, target);
            }
        }
    }
}
