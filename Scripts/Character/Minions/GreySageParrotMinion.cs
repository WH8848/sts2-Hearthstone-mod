using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 灰贤鹦鹉 (Grey Sage Parrot) - 吉安娜专属随从。
/// 属性：攻击 4，生命 5。战吼：重复你施放的上一个费用消耗大于等于 2 点的法术
/// （免费自动打出，随机目标）。
/// </summary>
[RegisterMonster]
public sealed class GreySageParrotMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 5;

    public override int MaxInitialHp => 5;

    protected override string MinionVisualsPath => "res://assets/card_art/grey_sage_parrot.png";

    /// <summary>
    /// 战吼：重复上一个费用 ≥ 2 的法术（按施放时的升级级别与衍生状态恢复，随机目标）。
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
        // 重复"我"施放的上一个费用 ≥ 2 的法术（按玩家区分，联机不误用队友的）
        if (!rec.LastCastSpellCost2PlusByPlayer.TryGetValue(owner.NetId, out var last) ||
            last is not { } played)
        {
            return;
        }
        var last2 = played;

        // 按施放时的升级级别与衍生状态创建副本（衍生卡保持"牌库之外"语义）
        var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, owner, last2.Type, last2.UpgradeLevel);
        if (card == null)
        {
            return;
        }
        if (last2.IsGenerated)
        {
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
        }

        // 需要目标时（单目标卡：原生 AnyEnemy/AnyPlayer/AnyAlly 或自定义单目标类型），
        // 按卡的目标校验从场上活物中随机选一个合法目标
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
        // AutoPlay：免费自动打出（不消耗能量），标记为自动打出（不计入"手打"计数）
        jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(card);
        await CardCmd.AutoPlay(choiceContext, card, target);
    }
}
