using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 吉安娜地标基类 - 占据一个随从槽位的地标单位。
/// - 不可攻击（攻击力恒为 0，不显示攻击意图），不参与随从军势挡刀（见 MinionSquadPower 过滤）；
/// - 每两个回合可点击使用一次：玩家点击地标 → 选择一名角色 → 触发 <see cref="OnLandmarkEffect"/>；
///   打出当回合即可使用（召唤时立即授予行动点）；
/// - 拥有耐久度（<see cref="LandmarkDurabilityPower"/>）：每次使用 -1，归零时地标被摧毁（离开战场）；
/// - 冷却（<see cref="LandmarkCooldownPower"/>）：每次使用后挂 2 层，玩家回合开始递减，
///   归零恢复（使用回合后的下一回合不可用，再下一回合恢复）；
/// - 重新开启（<see cref="Reactivate"/>）：移除冷却并立即重新授予行动点，当回合即可再次点击使用。
/// </summary>
public abstract class JainaLandmarkBase : JainaMinionBase
{
    /// <summary>
    /// 地标初始耐久度（每次使用 -1，归零时地标被摧毁）
    /// </summary>
    public abstract int LandmarkDurability { get; }

    /// <summary>
    /// 生命值 = 耐久度（地标生命值视觉显示耐久度：血条数值即剩余耐久；
    /// 地标免疫伤害，生命值只在使用地标时同步减少）。
    /// MinInitialHp/MaxInitialHp 仅作模型默认值，召唤时由 OnSummon 按耐久度覆盖。
    /// </summary>
    public override int MinInitialHp => 999;

    public override int MaxInitialHp => 999;

    /// <summary>
    /// 显示血条（数值 = 生命值 = 剩余耐久度）
    /// </summary>
    public override bool IsHealthBarVisible => true;

    /// <summary>
    /// 被召唤时初始化：设置高生命兜底、挂耐久度，并立即授予本回合的使用行动点
    /// （打出当回合即可使用；行动点回合末自动移除）。
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        var applier = owner.Creature;

        // 耐久度
        await PowerCmd.Apply<LandmarkDurabilityPower>(choiceContext, Creature, LandmarkDurability, applier, null);

        // 打出当回合即可使用：直接授予本回合的使用行动点（回合末自动移除，下回合按冷却重新授予）
        await PowerCmd.Apply<JainaLandmarkUseAction>(choiceContext, Creature, 1m, applier, null);

        RefreshIntentDisplay();
    }

    /// <summary>
    /// 玩家回合开始：冷却递减（归零移除并恢复可用），冷却结束后授予本回合的使用行动点。
    /// 地标不调用基类实现（基类授予的是攻击行动点，地标不可攻击）。
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Player || !Creature.IsAlive)
        {
            return;
        }
        // 召唤当回合的回合开始回调（如回合开始阶段被召唤）不再重复授予行动点：
        // 行动点已在 OnSummon 授予（打出当回合可用）
        if (IsSummonedThisTurn())
        {
            return;
        }

        var cooldown = Creature.GetPower<LandmarkCooldownPower>();
        if (cooldown != null)
        {
            if (cooldown.Amount <= 1)
            {
                // 冷却归零：本回合恢复可用（继续走下面的授予逻辑）
                await PowerCmd.Remove(cooldown);
            }
            else
            {
                // 仍在冷却中：递减，本回合不可用
                await PowerCmd.Decrement(cooldown);
                RefreshIntentDisplay();
                return;
            }
        }

        // 冷却结束（或无冷却）：授予本回合的使用行动点
        var applier = Creature.PetOwner?.Creature ?? Creature;
        await PowerCmd.Apply<JainaLandmarkUseAction>(choiceContext, Creature, 1m, applier, null);
        RefreshIntentDisplay();
    }

    /// <summary>
    /// 玩家点击地标使用：触发具体效果 → 耐久 -1（归零摧毁地标）→ 挂 2 层冷却。
    /// 由 <see cref="JainaLandmarkUseAction.OnAct"/> 调用。
    /// </summary>
    public async Task PerformUse(PlayerChoiceContext choiceContext, Creature target)
    {
        if (!Creature.IsAlive)
        {
            return;
        }

        // 具体效果（冻结/召唤等）
        await OnLandmarkEffect(choiceContext, target);
        if (!Creature.IsAlive)
        {
            return;
        }

        // 耐久 -1；归零时地标被摧毁（离开战场）
        var durability = Creature.GetPower<LandmarkDurabilityPower>();
        if (durability != null)
        {
            if (durability.Amount <= 1)
            {
                await PowerCmd.Remove(durability);
                await CreatureCmd.Kill(Creature, force: true);
                return;
            }
            await PowerCmd.Decrement(durability);
            // 生命值视觉同步：HP = 剩余耐久度（血条显示耐久）
            await CreatureCmd.SetMaxAndCurrentHp(Creature, durability.Amount);
        }

        // 冷却 2 层：每两个回合可点击使用一次（使用回合后的下一回合不可用，再下一回合恢复）
        var applier = Creature.PetOwner?.Creature ?? Creature;
        await PowerCmd.Apply<LandmarkCooldownPower>(choiceContext, Creature, 2m, applier, null);
        RefreshIntentDisplay();
    }

    /// <summary>
    /// 重新开启地标（"施放法术后重新开启"等效果用）：
    /// 移除冷却，并立即重新授予使用行动点——开启后当回合即可再次点击使用，无需等到下回合。
    /// 若地标当前已有可用行动点（未使用过），不重复授予。
    /// </summary>
    public async Task Reactivate(PlayerChoiceContext choiceContext)
    {
        if (!Creature.IsAlive)
        {
            return;
        }
        var cooldown = Creature.GetPower<LandmarkCooldownPower>();
        if (cooldown != null)
        {
            await PowerCmd.Remove(cooldown);
        }
        if (Creature.GetPower<JainaLandmarkUseAction>() == null)
        {
            var applier = Creature.PetOwner?.Creature ?? Creature;
            await PowerCmd.Apply<JainaLandmarkUseAction>(choiceContext, Creature, 1m, applier, null);
        }
        RefreshIntentDisplay();
    }

    /// <summary>
    /// 地标不可被上任何状态（炉石规则：地标不受任何状态影响,只通过点击使用消耗耐久度）。
    /// 全部正面(Buff)与全部负面(Debuff:灾厄/中毒/冻结/虚弱/易伤/力量/狂怒等)施加一律 0;
    /// <b>仅放行地标自身机制所需的内建状态</b>：
    /// 耐久度/冷却/使用行动点/随从标记(MinionPower)。
    /// 机制与法术反制拦敌人 Power 同款（TryModifyPowerAmountReceived 全局施加入口）。
    /// </summary>
    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target,
        decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        // 地标内建状态放行（不拦截）
        if (canonicalPower is LandmarkDurabilityPower or
            LandmarkCooldownPower or
            JainaLandmarkUseAction or
            MegaCrit.Sts2.Core.Models.Powers.MinionPower)
        {
            return false;
        }
        // 其余一切状态（正面+负面）数量改 0
        modifiedAmount = 0m;
        return true;
    }

    /// <summary>
    /// 地标使用时的目标类型（默认 None：点击直接触发，不选目标）。
    /// 需要选择目标的地标（如夜隐者圣所冻结目标）覆写为对应目标类型。
    /// </summary>
    public virtual MegaCrit.Sts2.Core.Entities.Cards.TargetType UseTargetType =>
        MegaCrit.Sts2.Core.Entities.Cards.TargetType.None;

    /// <summary>
    /// 地标使用效果（点击使用地标时触发）。子类实现具体效果。
    /// </summary>
    /// <param name="target">点击时选择的目标角色（无目标地标为 null）</param>
    public abstract Task OnLandmarkEffect(PlayerChoiceContext choiceContext, Creature target);
}
