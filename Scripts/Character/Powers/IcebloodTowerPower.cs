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
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 冰血哨塔：在你的回合结束时，从抽牌堆中抽一张法术牌并打出；
/// 抽牌堆中没有法术时，从弃牌堆中抽一张法术牌并打出。
/// 可叠层：每张冰血哨塔在回合结束各触发一次（Amount = 哨塔数量）。
/// 挂在吉安娜玩家身上（可见）。
/// </summary>
[RegisterPower]
public sealed class IcebloodTowerPower : PowerModel, IModPowerAssetOverrides
{
    /// <inheritdoc />
    public PowerAssetProfile AssetProfile => new("res://assets/power_icons/jaina_power_iceblood_tower_power.png");

    /// <inheritdoc />
    public string? CustomIconPath => AssetProfile.IconPath;

    /// <inheritdoc />
    public string? CustomBigIconPath => AssetProfile.BigIconPath;

    public override PowerType Type => PowerType.Buff;

    /// <summary>
    /// 可叠层：多张冰血哨塔每回合结束各触发一次（Amount = 层数）
    /// </summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 玩家回合结束：每层冰血哨塔从抽牌堆抽一张法术牌并打出
    /// （抽牌堆没有法术时从弃牌堆抽；打出后进弃牌堆——不被消耗）。
    /// </summary>
    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (Owner == null || Owner.Side != side)
        {
            return;
        }
        var player = Owner.Player;
        if (player == null || player.PlayerCombatState == null)
        {
            return;
        }
        var combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // 每层哨塔各触发一次（Amount = 哨塔数量）
        int casts = System.Math.Max(1, (int)Amount);
        for (int c = 0; c < casts; c++)
        {
            var card = PickSpellFromPiles(player);
            if (card == null)
            {
                return;
            }

            // 单目标牌：从场上所有活物中随机选一个合法目标（联机可打队友）
            Creature? target = null;
            if (card.TargetType == TargetType.AnyEnemy || card.TargetType == TargetType.AnyPlayer ||
                card.TargetType == TargetType.AnyAlly ||
                (CustomTargetTypeManager.TryGetCustomTargetType(card.TargetType, out var customType) &&
                 customType.IsSingleTarget))
            {
                var pool = combatState.Creatures
                    .Where(c => c != null && c.IsAlive && card.IsValidTarget(c))
                    .ToList();
                target = pool.Count > 0 ? player.RunState.Rng.CombatTargets.NextItem(pool) : null;
                if (target == null)
                {
                    continue;
                }
            }

            // 哨塔打出的法术不被消耗：移除消耗词条（如有），打出后进弃牌堆
            // （GetResultLocationForCardPlay 按 Exhaust 关键词决定去向）。
            // 注意：从抽牌堆/弃牌堆选的是原卡实例——移除 Exhaust 会永久改变该卡，
            // 符合"哨塔释放的法术不消耗、可再次被抽到"的语义。
            if (card.Keywords.Contains(CardKeyword.Exhaust))
            {
                card.RemoveKeyword(CardKeyword.Exhaust);
            }
            await CardCmd.AutoPlay(choiceContext, card, target);
        }
    }

    /// <summary>
    /// 从抽牌堆中随机选一张法术牌（攻击/技能牌，不含英雄技能卡）；
    /// 抽牌堆中没有法术时，从弃牌堆中随机选一张法术牌。
    /// </summary>
    private static CardModel? PickSpellFromPiles(Player player)
    {
        // 抽牌堆中的法术牌（攻击/技能牌，不含英雄技能卡）
        var spells = player.PlayerCombatState?.DrawPile.Cards
            .Where(IsSpellCard)
            .ToList();
        if (spells is { Count: > 0 })
        {
            return player.RunState.Rng.CombatTargets.NextItem(spells);
        }

        // 抽牌堆无法术：从弃牌堆中随机选一张法术牌
        var discardSpells = player.PlayerCombatState?.DiscardPile.Cards
            .Where(IsSpellCard)
            .ToList();
        if (discardSpells is { Count: > 0 })
        {
            return player.RunState.Rng.CombatTargets.NextItem(discardSpells);
        }
        return null;
    }

    /// <summary>
    /// 是否为可被哨塔施放的法术牌（攻击/技能牌，不含英雄技能卡）
    /// </summary>
    private static bool IsSpellCard(CardModel card)
    {
        return card != null &&
               (card.Type == CardType.Attack || card.Type == CardType.Skill) &&
               !HeroPowerHandHelper.IsHeroPowerCard(card);
    }
}
