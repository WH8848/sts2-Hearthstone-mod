using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 戏法图腾：在你的回合结束时，随机施放一个费用消耗小于或等于1点的全角色卡牌。
/// 挂在吉安娜玩家身上（可见）。
/// </summary>
[RegisterPower]
public sealed class TrickTotemPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_trick_totem_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 可叠层：多张戏法图腾每回合结束各施放一次（Amount = 层数）
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 玩家回合结束：每层戏法图腾随机施放一个费用消耗 <= 1 的全角色卡牌
    /// （攻击/技能牌，不含英雄技能卡；按可升级级别展开；单目标随机选合法目标）。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner == null || Owner.Side != side)
        {
            return;
        }
        var player = Owner.Player;
        if (player == null || player.Creature?.CombatState == null)
        {
            return;
        }
        var combatState = player.Creature.CombatState;
        var rng = player.RunState.Rng.CombatTargets;

        // 全角色卡牌候选：所有攻击/技能牌（不含英雄技能卡）且费用消耗 <= 1，
        // 按可升级级别展开（未升级与升级形态都可能被施放）
        var candidates = new List<CardModel>();
        foreach (var canonical in ModelDb.AllCards)
        {
            if (canonical == null)
            {
                continue;
            }
            if (canonical.Type != CardType.Attack && canonical.Type != CardType.Skill)
            {
                continue;
            }
            if (HeroPowerHandHelper.IsHeroPowerCard(canonical))
            {
                continue;
            }
            int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(canonical.GetType());
            for (int level = 0; level <= maxLevel; level++)
            {
                var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                    combatState, player, canonical.GetType(), level);
                if (card != null && card.EnergyCost.Canonical <= 1)
                {
                    candidates.Add(card);
                }
            }
        }
        if (candidates.Count == 0)
        {
            return;
        }
        // 每层戏法图腾各施放一次（Amount = 层数）
        int casts = Math.Max(1, (int)Amount);
        for (int c = 0; c < casts; c++)
        {
            var spell = rng.NextItem(candidates);
            if (spell == null)
            {
                return;
            }
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(spell);
            jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(spell);

            // 单目标牌：从场上所有活物（含自己/队友角色与双方随从）中随机选一个合法目标
            Creature? target = null;
            if (spell.TargetType != TargetType.None)
            {
                var pool = GetAllAliveCreatures(combatState)
                    .Where(c => spell.IsValidTarget(c))
                    .ToList();
                target = pool.Count > 0 ? rng.NextItem(pool) : null;
                if (target == null)
                {
                    return;
                }
            }
            await CardCmd.AutoPlay(choiceContext, spell, target);
        }
    }

    /// <summary>
    /// 全部存活生物（敌人 + 所有玩家角色 + 双方随从）
    /// </summary>
    private static IEnumerable<Creature> GetAllAliveCreatures(ICombatState combatState)
    {
        var list = combatState.Creatures
            .Where(c => c != null && c.IsAlive)
            .ToList();
        foreach (var player in combatState.Players)
        {
            list.AddRange(player.PlayerCombatState?.Pets.Where(p => p != null && p.IsAlive) ?? []);
        }
        return list;
    }
}
