using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Keywords;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 维萨鲁斯 (Vexallus) - 吉安娜专属随从。
/// 属性：攻击 3，生命 5。你的奥术法术会施放两次。
/// </summary>
[RegisterMonster]
public sealed class VexallusMinion : JainaMinionBase
{
    /// <summary>
    /// 防递归标志：第二次施放（重放副本）不再触发"施放两次"。
    /// </summary>
    private static bool _replaying;

    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 5;

    public override int MaxInitialHp => 5;

    /// <summary>
    /// 战斗视觉：维萨鲁斯卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/vexallus.png";

    /// <summary>
    /// 你的奥术法术会施放两次：施放奥术法术后，生成同类型副本（保留升级级别）
    /// 免费再次自动施放；单目标牌从场上所有活物中随机选合法目标（联机可打队友）。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_replaying)
        {
            return;
        }
        if (!Creature.IsAlive || Creature.PetOwner == null || cardPlay.Card.Owner != Creature.PetOwner)
        {
            return;
        }
        var card = cardPlay.Card;
        // 只统计法术牌（攻击牌和技能牌）且带奥术派系关键词
        if (card.Type != CardType.Attack && card.Type != CardType.Skill)
        {
            return;
        }
        if (!card.Keywords.Contains(JainaKeywords.Arcane))
        {
            return;
        }

        var owner = Creature.PetOwner;
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 生成同类型副本（恢复升级级别），免费再次施放
        var copy = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, owner, card.GetType(), card.CurrentUpgradeLevel);
        if (copy == null)
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(copy);

        // 单目标牌：从场上所有活物中随机选一个合法目标
        Creature? target = null;
        if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyPlayer ||
            card.TargetType == TargetType.AnyAlly ||
            (CustomTargetTypeManager.TryGetCustomTargetType(card.TargetType, out var customType) &&
             customType.IsSingleTarget))
        {
            var pool = combatState.Creatures
                .Where(c => c != null && c.IsAlive && copy.IsValidTarget(c))
                .ToList();
            target = pool.Count > 0 ? owner.RunState.Rng.CombatTargets.NextItem(pool) : null;
            if (target == null)
            {
                return;
            }
        }
        // 标记自动打出（不计入"手打"计数，避免重放自身膨胀）
        jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(copy);
        _replaying = true;
        try
        {
            await CardCmd.AutoPlay(choiceContext, copy, target);
        }
        finally
        {
            _replaying = false;
        }
    }
}
