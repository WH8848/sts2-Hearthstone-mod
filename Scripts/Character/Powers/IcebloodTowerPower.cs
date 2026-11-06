using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 冰血哨塔：在你的回合结束时，随机从抽牌堆中施放另一个法术。持续 3 回合。
/// Amount = 剩余回合数（每次触发 -1，归零移除）。
/// </summary>
[RegisterPower]
public sealed class IcebloodTowerPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => true;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        var player = Owner?.Player;
        if (player == null || Owner.Side != side)
        {
            return;
        }

        try
        {
            var drawPile = player.PlayerCombatState?.DrawPile;
            if (drawPile != null)
            {
                // "另一个法术"：抽牌堆中的攻击/技能类法术牌（带"法术牌"关键词）。
                // 冰血哨塔自身是能力牌，不会进入该池。
                var spells = drawPile.Cards
                    .Where(c => c != null
                                && (c.Type == CardType.Attack || c.Type == CardType.Skill)
                                && c.Keywords.Contains(JainaKeywords.Spell))
                    .ToList();
                if (spells.Count > 0)
                {
                    var chosen = player.RunState.Rng.CombatCardSelection.NextItem(spells);
                    if (chosen != null)
                    {
                        Creature? target = ResolveRandomTarget(chosen, player);
                        await CardCmd.AutoPlay(choiceContext, chosen, target);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] Iceblood Tower cast failed: {ex}");
        }

        int next = Amount - 1;
        if (next <= 0)
        {
            await PowerCmd.Remove(this);
        }
        else
        {
            SetAmount(next);
        }
    }

    /// <summary>
    /// 为随机施放的法术选择合法目标（优先敌人；无目标需求时返回 null）。
    /// </summary>
    private static Creature? ResolveRandomTarget(CardModel card, Player player)
    {
        var combatState = player.Creature?.CombatState;
        if (combatState == null || player.Creature == null)
        {
            return null;
        }
        if (card.TargetType != TargetType.AnyEnemy
            && card.TargetType != TargetType.AnyPlayer
            && card.TargetType != TargetType.AnyAlly)
        {
            return null;
        }
        var pool = combatState.Creatures
            .Where(c => c != null && c.IsAlive && card.IsValidTarget(c))
            .ToList();
        var enemies = pool.Where(c => c.Side != player.Creature.Side).ToList();
        if (enemies.Count > 0)
        {
            pool = enemies;
        }
        return pool.Count > 0 ? player.RunState.Rng.CombatTargets.NextItem(pool) : null;
    }
}
