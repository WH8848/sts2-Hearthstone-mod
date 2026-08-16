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
/// 鲁莽的学徒 (Reckless Apprentice) - 吉安娜专属随从。
/// 属性：攻击 3，生命 5。
/// 战吼：向随机敌人发射 8 次你的英雄技能（免费自动打出当前英雄技能，随机敌人目标）。
/// </summary>
[RegisterMonster]
public sealed class RecklessApprenticeMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 5;

    public override int MaxInitialHp => 5;

    /// <summary>
    /// 战斗视觉：鲁莽的学徒卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/reckless_apprentice.png";

    /// <summary>
    /// 战吼：向随机敌人发射 8 次你的英雄技能。
    /// 每次免费自动打出当前英雄技能（默认火焰冲击或英雄卡替换后的技能）的副本——
    /// 副本从手牌中同类型的英雄技能卡取升级等级，打出后即移出牌堆：
    /// 手牌中的英雄技能卡不受影响，不会额外生成/堆叠英雄技能卡。仅手牌打出时触发。
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
        // 当前英雄技能类型（null = 默认火焰冲击）
        var heroPowerType = rec.CurrentHeroPowerType ?? typeof(Cards.Fireblast);

        for (int i = 0; i < 8; i++)
        {
            // 副本：与手牌中的英雄技能同一张（同类型同升级等级），不标记衍生/消耗
            var heroPower = jaina.Scripts.Character.JainaCastTracker.CreateHeroPowerCopy(
                combatState, owner, heroPowerType);
            if (heroPower == null)
            {
                continue;
            }

            // 单目标英雄技能：随机选合法敌人目标
            Creature? target = null;
            if (heroPower.TargetType == TargetType.AnyEnemy || heroPower.TargetType == TargetType.AnyPlayer ||
                heroPower.TargetType == TargetType.AnyAlly ||
                (CustomTargetTypeManager.TryGetCustomTargetType(heroPower.TargetType, out var customType) &&
                 customType.IsSingleTarget))
            {
                var pool = combatState.Creatures
                    .Where(c => c != null && c.IsAlive && heroPower.IsValidTarget(c) &&
                                c.Side != owner.Creature.Side)
                    .ToList();
                target = pool.Count > 0 ? owner.RunState.Rng.CombatTargets.NextItem(pool) : null;
                if (target == null)
                {
                    continue;
                }
            }
            await CardCmd.AutoPlay(choiceContext, heroPower, target);

            // 副本打出后从牌堆移除：不回到手牌、不产生额外英雄技能卡
            if (heroPower.Pile != null)
            {
                heroPower.RemoveFromCurrentPile(silent: true);
            }
        }
    }
}
