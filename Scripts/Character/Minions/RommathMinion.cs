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

    protected override string MinionVisualsPath => "res://assets/minion_visuals/archmage_rommath.tscn";

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
        var hittable = combatState.HittableEnemies.Where(e => e != null && e.IsAlive).ToList();
        foreach (var type in types)
        {
            var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(type));
            if (canonical == null)
            {
                continue;
            }
            var card = combatState.CreateCard(canonical, owner);

            // 需要目标时随机选择可命中敌人（AnyEnemy/自定义 AnyCreature）
            Creature? target = null;
            if (card.TargetType == TargetType.AnyEnemy || card.TargetType == MinionTargetTypes.AnyCreature)
            {
                target = hittable.Count > 0 ? owner.RunState.Rng.CombatTargets.NextItem(hittable) : null;
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
