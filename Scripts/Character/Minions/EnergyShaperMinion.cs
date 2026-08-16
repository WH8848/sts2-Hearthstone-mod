using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 能量塑形师 (Energy Shaper) - 吉安娜专属随从。
/// 属性：攻击 3，生命 4。
/// 战吼：将你手牌中的所有法术牌变形成为费用增加1点的法术牌。（保留其原始费用。）
/// 仅手牌打出时触发。
/// </summary>
[RegisterMonster]
public sealed class EnergyShaperMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    /// <summary>
    /// 战斗视觉：能量塑形师卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/energy_shaper.png";

    /// <summary>
    /// 战吼：将手牌中所有法术牌（攻击/技能牌，不含英雄技能卡）变形为
    /// 一张随机法术牌，其原始费用 = 原牌费用 + 1；变形后的牌保留原牌费用显示。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        var hand = PileType.Hand.GetPile(owner);
        if (hand == null)
        {
            return;
        }

        // 快照手牌中的法术牌（排除英雄技能卡：火焰冲击等英雄技能不应被变形）
        var spells = hand.Cards
            .Where(c => c != null &&
                        (c.Type == CardType.Attack || c.Type == CardType.Skill) &&
                        !HeroPowerHandHelper.IsHeroPowerCard(c))
            .ToList();
        if (spells.Count == 0)
        {
            return;
        }

        var rng = owner.RunState.Rng.CombatCardSelection;
        var combatState = Creature.CombatState;

        foreach (var spell in spells)
        {
            if (spell == null || spell.Pile == null || !spell.IsTransformable)
            {
                continue;
            }
            // 原牌原始费用
            int originalCost = spell.EnergyCost.Canonical;
            // 目标池：所有法术牌（攻击/技能牌）中原始费用 = 原费用 + 1 的牌
            var candidates = ModelDb.AllCards
                .Where(c => c != null &&
                            (c.Type == CardType.Attack || c.Type == CardType.Skill) &&
                            c.EnergyCost.Canonical == originalCost + 1 &&
                            !HeroPowerHandHelper.IsHeroPowerCard(c))
                .ToList();
            if (candidates.Count == 0)
            {
                continue;
            }
            var chosen = rng.NextItem(candidates);
            if (chosen == null)
            {
                continue;
            }

            // 生成带 Owner 的变形目标实例（Transform 要求 replacement.Owner == original.Owner）
            var replacement = combatState?.CreateCard(chosen, owner);
            if (replacement == null)
            {
                continue;
            }
            // 保留原始费用：变形后的牌仍显示原牌费用
            replacement.EnergyCost.SetCustomBaseCost(originalCost);

            await CardCmd.Transform(spell, replacement);
        }
    }
}
