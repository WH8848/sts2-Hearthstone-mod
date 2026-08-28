using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 绿洲盟军：吉安娜或其随从受到攻击时，召唤一个 3/6 的水元素，随后消失。
/// 只要被敌方攻击就触发——哪怕伤害被格挡（零点伤害）也触发（同火焰结界口径）。
/// 一次性结界（参照火焰结界模式）：受击触发后移除；若整回合未被攻击，
/// 下个玩家回合开始兜底移除。
/// </summary>
[RegisterPower]
public sealed class OasisAllyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>
    /// 隐藏：召唤提示由卡面效果本身展示（无图标，同 PlayedRecordHookPower 风格）
    /// </summary>
    protected override bool IsVisibleInternal => false;

    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // target 是自己，或是自己的随从（随从受击伤害转入主人护甲）
        bool isOwnerOrPet = target == Owner || target.PetOwner?.Creature == Owner;
        // 只有敌方造成的伤害才算"受到攻击"：吉安娜自己/己方效果对随从造成的伤害不触发
        bool isEnemyDamage = dealer != null && dealer.Side != Owner.Side;
        // 只要被敌方攻击就触发——哪怕伤害被格挡（零点伤害）也触发（用户口径）
        if (!isOwnerOrPet || !isEnemyDamage || Amount <= 0)
        {
            return;
        }

        // 召唤一个 3/6 的水元素（满场放不下则不召唤，结界仍消失）
        var owner = Owner.Player;
        if (owner != null)
        {
            await jaina.Scripts.Character.Minions.JainaMinionPool.SummonMinionByType(
                choiceContext, owner, typeof(jaina.Scripts.Character.Minions.WaterElementalMinion),
                maxHp: 6, attack: 3);
        }

        // 一次性结界：触发后消失
        await PowerCmd.Remove(this);
    }

    /// <summary>
    /// 下一个玩家回合开始时移除（未触发的兜底——同火焰结界模式）。
    /// await 而非 fire-and-forget：避免移除的 AfterRemoved 钩子（push/pop 模型）
    /// 与当前钩子链交错导致"Tried to pop model"模型栈警告。
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && Amount > 0)
        {
            await PowerCmd.Remove(this);
        }
    }
}
