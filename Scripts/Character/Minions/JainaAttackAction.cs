using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using MinionLib.Action;
using MinionLib.Commands;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 手动模式随从的攻击行动点（MinionLib ActionModel 行动系统）。
/// 玩家点击随从（或行动图标）→ 选择一名敌人 → 随从冲撞攻击，造成其攻击力点伤害。
/// Amount = 本回合可用行动次数；每次行动扣 1 点，回合结束自动消失，
/// 由 <see cref="JainaMinionBase"/> 在每回合开始时重新授予。
/// </summary>
[RegisterPower]
public sealed class JainaAttackAction : ActionModel
{
    public override TargetType TargetType => TargetType.AnyEnemy;

    /// <summary>
    /// 行动点是当回合有效的，回合结束自动移除
    /// </summary>
    public override bool AutoRemoveAtTurnEnd => true;

    /// <summary>
    /// 每次行动后消耗 1 点行动次数
    /// </summary>
    public override bool DecrementAfterAct => true;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override async Task OnAct(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (target == null || !Owner.IsAlive)
        {
            return;
        }

        // 攻击力取随从的 BaseAttackValue（与自动模式一致，不吃力量加成）
        var minion = Owner.Monster as JainaMinionBase;
        if (minion == null)
        {
            return;
        }
        var attack = minion.BaseAttackValue;
        if (attack <= 0)
        {
            return;
        }

        var actor = Owner;
        await MinionAnimCmd.PlayBumpAttackAsync(actor, target,
            () => CreatureCmd.Damage(choiceContext, [target], attack, ValueProp.Unpowered, actor));

        // 行动次数由 MinionLib 框架在 OnAct 之后自动递减（DecrementAfterAct → PowerCmd.Decrement），
        // 递减会触发意图刷新；此处再主动刷新一次意图显示保证即时隐藏（幂等）。
        minion.RefreshIntentDisplay();
    }
}
