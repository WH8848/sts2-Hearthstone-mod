using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Powers;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 小精灵驾驭者 (Imp Wrangler) - 吉安娜专属随从。
/// 属性：攻击 4，生命 4。
/// 战吼：灌注并触发你的英雄技能（免费自动打出当前英雄技能，随机目标）。
/// </summary>
[RegisterMonster]
public sealed class ImpWranglerMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    /// <summary>
    /// 战斗视觉：小精灵驾驭者卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/imp_wrangler.png";

    /// <summary>
    /// 战吼：灌注（+1 层）并触发你的英雄技能。
    /// 触发方式：免费自动对随机敌人打出当前英雄技能的副本——
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

        // 1) 灌注：英雄技能伤害 +1（与灵体采集者同款）
        await PowerCmd.Apply<EmpowerPower>(choiceContext, [owner.Creature], 1m, Creature, null);

        // 2) 触发你的英雄技能：免费自动对随机敌人打出当前英雄技能的副本
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        // 当前英雄技能类型（null = 默认火焰冲击；按玩家区分，联机用我自己的）
        var heroPowerType = rec.CurrentHeroPowerTypeByPlayer.TryGetValue(owner.NetId, out var hpt)
            ? hpt ?? typeof(Cards.Fireblast)
            : typeof(Cards.Fireblast);
        // 副本：与手牌中的英雄技能同一张（同类型同升级等级），不标记衍生/消耗
        var heroPower = jaina.Scripts.Character.JainaCastTracker.CreateHeroPowerCopy(
            combatState, owner, heroPowerType);
        if (heroPower == null)
        {
            return;
        }

        // 单目标英雄技能：随机选合法目标（尽可能以敌人为目标）
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
                return;
            }
        }
        await CardCmd.AutoPlay(choiceContext, heroPower, target);

        // 3) 副本打出后从牌堆移除：不回到手牌、不产生额外英雄技能卡
        if (heroPower.Pile != null)
        {
            heroPower.RemoveFromCurrentPile(silent: true);
        }
    }
}
