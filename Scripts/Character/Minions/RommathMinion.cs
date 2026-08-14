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
    /// 战吼：重放本局施放过的每张牌库之外的攻击/技能牌（免费自动打出，随机目标）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        var combatState = Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        // 快照遍历，避免重放触发的新记录影响本循环
        var types = rec.GeneratedAttackSkills.ToList();
        foreach (var type in types)
        {
            var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(type));
            if (canonical == null)
            {
                continue;
            }
            var card = combatState.CreateCard(canonical, owner);

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
                    .Where(c => c != null && c.IsAlive && card.IsValidTarget(c)
                                // 重放为 AI 随机选目标：排除施法者本体（不会火球打自己）
                                && !(c.Side == MegaCrit.Sts2.Core.Combat.CombatSide.Player && !c.IsPet))
                    .ToList();
                target = pool.Count > 0 ? owner.RunState.Rng.CombatTargets.NextItem(pool) : null;
                if (target == null)
                {
                    continue;
                }
            }
            // AutoPlay：免费自动打出（不消耗能量），随机目标语义已由上方处理
            await CardCmd.AutoPlay(choiceContext, card, target);
        }
    }
}
