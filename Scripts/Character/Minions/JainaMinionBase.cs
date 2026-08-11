using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;
using MinionLib.Minion;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 吉安娜随从基类 - 真正的生物单位。
/// 属性：攻击力（上方数字）、生命值（下方数字）。
/// 不显示血条和意图，视觉使用闪电充能球模型，固定在玩家身边。
/// 两种行为模式（<see cref="JainaMinionBehaviorMode"/>）：
/// - 手动模式（默认）：随从永不自动行动，一切行动靠玩家点击随从触发（行动点制）。
/// - 自动模式：玩家回合结束时自动攻击随机敌人，并执行各随从独有被动。
/// </summary>
public abstract class JainaMinionBase : MinionModel
{
    /// <summary>
    /// 随从基础攻击力（通过 MinionSummonOptions.PrimaryStatAmount 传入实际值）
    /// </summary>
    public int BaseAttackValue = 0;

    /// <summary>
    /// 随从行为模式（默认手动：不自动行动，点击驱动）
    /// </summary>
    public virtual JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    /// <summary>
    /// 手动模式下每回合可点击攻击的次数（默认 1 次）
    /// </summary>
    public virtual int ActionsPerTurn => 1;

    /// <summary>
    /// 统一使用闪电充能球战斗视觉（临时方案，后续可替换为专属随从动画）
    /// </summary>
    protected override string VisualsPath => SceneHelper.GetScenePath("orbs/orb_visuals/lightning_orb");

    /// <summary>
    /// 随从不显示血条
    /// </summary>
    public override bool IsHealthBarVisible => false;

    /// <summary>
    /// 随从不显示在怪物图鉴中
    /// </summary>
    public override bool ShouldShowInCompendium => false;

    /// <summary>
    /// 随从死亡后从战斗场景移除（生命值为零时消失）
    /// </summary>
    public override bool ShouldDisappearFromDoom => true;

    /// <summary>
    /// 随从意图与行动状态机。
    /// 自动模式：恒定攻击意图，伤害 = 攻击力，随从每次敌方回合都会尝试攻击。
    /// 手动模式：纯 IDLE 状态机（与 MinionLib 默认一致），随从永不自动行动，一切靠玩家点击。
    /// </summary>
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        // 手动模式：IDLE 状态机，无任何意图，随从不会在自己的回合自动行动
        if (BehaviorMode == JainaMinionBehaviorMode.Manual)
        {
            var idle = new MoveState("MINION_IDLE", _ => Task.CompletedTask)
            {
                FollowUpState = null
            };
            idle.FollowUpState = idle; // 循环自身
            return new MonsterMoveStateMachine([idle], idle);
        }

        // 自动模式：延迟读取 BaseAttackValue，确保召唤时的攻击设定生效
        var attackMove = new MoveState(
            "MINION_ATTACK",
            async targets =>
            {
                var target = targets.FirstOrDefault();
                if (target == null || !Creature.IsAlive) return;
                await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), [target], BaseAttackValue, ValueProp.Unpowered, Creature);
            },
            new SingleAttackIntent(() => BaseAttackValue))
        {
            FollowUpState = null
        };
        attackMove.FollowUpState = attackMove; // 循环自身，意图恒定

        return new MonsterMoveStateMachine([attackMove], attackMove);
    }

    /// <summary>
    /// 被召唤时初始化：设置生命/攻击，应用随从副单位标记（不触发击杀胜利结算）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        if (options.MaxHp is decimal maxHp)
        {
            await CreatureCmd.SetMaxAndCurrentHp(Creature, maxHp);
        }
        if (options.PrimaryStatAmount is decimal attack && attack > 0m)
        {
            BaseAttackValue = (int)attack;
        }

        // 标记为随从副单位（不触发击杀胜利结算、死亡不触发致命等）
        await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.MinionPower>(
            choiceContext, Creature, 1m, owner.Creature, options.Source);
    }

    /// <summary>
    /// 玩家回合开始时：手动模式授予本回合的点击攻击行动点。
    /// （自动模式无需授予，随从会自行攻击。）
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (BehaviorMode != JainaMinionBehaviorMode.Manual || side != CombatSide.Player || !Creature.IsAlive)
        {
            return;
        }

        // 行动点由随从主人施加，Amount = 本回合可点击攻击次数
        var applier = Creature.PetOwner?.Creature ?? Creature;
        await PowerCmd.Apply<JainaAttackAction>(choiceContext, Creature, ActionsPerTurn, applier, null);
    }

    /// <summary>
    /// 玩家回合结束时：
    /// 自动模式 - 攻击力 > 0 的随从对随机可命中敌人造成攻击力点伤害；
    /// 手动模式 - 随从不自动行动（靠玩家点击）；
    /// 两种模式都会执行随从独有回合结束被动。
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player)
        {
            return;
        }
        if (!Creature.IsAlive)
        {
            return;
        }

        if (BehaviorMode == JainaMinionBehaviorMode.Auto)
        {
            await PerformTurnEndAttack(choiceContext);
        }
        await PerformTurnEndPassive(choiceContext);
    }

    /// <summary>
    /// 回合结束攻击：对随机可命中敌人造成攻击力点伤害。
    /// </summary>
    private async Task PerformTurnEndAttack(PlayerChoiceContext choiceContext)
    {
        if (BaseAttackValue <= 0 || Creature == null || Creature.CombatState == null)
        {
            return;
        }
        var opponents = Creature.CombatState
            .GetOpponentsOf(Creature)
            .Where(e => e != null && e.IsAlive && e.IsHittable)
            .ToList();
        if (opponents.Count == 0)
        {
            return;
        }
        var target = CombatState.RunState.Rng.CombatTargets.NextItem(opponents);
        if (target == null)
        {
            return;
        }
        await CreatureCmd.Damage(choiceContext, [target], BaseAttackValue, ValueProp.Unpowered, Creature);
    }

    /// <summary>
    /// 各随从独有的回合结束被动（基类默认为空）。
    /// </summary>
    protected virtual Task PerformTurnEndPassive(PlayerChoiceContext choiceContext) => Task.CompletedTask;

    /// <summary>
    /// 受到伤害后：若随从死亡，触发亡语并从战斗清理。
    /// 这是比挡伤钩子更可靠的死亡挂点（在挡伤吸收之后运行）。
    /// </summary>
    public override async Task AfterDamageReceivedLate(PlayerChoiceContext choiceContext, Creature target, MegaCrit.Sts2.Core.Entities.Creatures.DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Creature || Creature.IsAlive || Creature.CombatState == null)
        {
            return;
        }

        // 死亡：先触发亡语
        if (HasDeathrattle)
        {
            try
            {
                await OnDeathrattle(choiceContext);
            }
            catch
            {
                // 亡语失败不影响战斗
            }
        }

        // 从战斗状态与战斗管理器清理，保证从场上消失
        Creature.CombatState.RemoveCreature(Creature);
        MegaCrit.Sts2.Core.Combat.CombatManager.Instance?.RemoveCreature(Creature);
    }

    /// <summary>
    /// 是否拥有亡语词条（子类设置为 true 时，随从死亡会触发 <see cref="OnDeathrattle"/>）。
    /// </summary>
    public virtual bool HasDeathrattle => false;

    /// <summary>
    /// 亡语效果：随从死亡时触发。子类重写以实现具体效果。
    /// </summary>
    public virtual Task OnDeathrattle(PlayerChoiceContext choiceContext) => Task.CompletedTask;
}
