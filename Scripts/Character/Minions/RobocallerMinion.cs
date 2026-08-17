using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 拨号机器人 (Robocaller) - 吉安娜专属随从。
/// 属性：攻击 3，生命 2。
/// 战吼：随机拨号（三个 0~4 的数字，可重复），从抽牌堆定向抽取费用消耗等于
/// 每个拨号数字的牌各一张（每回合随机拨号！）。
/// 注意：X 费卡（CostsX）费用不定，不会被拨号抽中。
/// </summary>
[RegisterMonster]
public sealed class RobocallerMinion : JainaMinionBase
{
    /// <summary>
    /// 战斗视觉：拨号机器人卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/robocaller.png";

    public override int MinInitialHp => 2;

    public override int MaxInitialHp => 2;

    /// <summary>
    /// 战吼：随机拨号三个 0~4 数字，从抽牌堆各抽取一张费用匹配的牌。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        var drawPile = owner.PlayerCombatState?.DrawPile;
        if (drawPile == null)
        {
            return;
        }
        var rng = owner.RunState.Rng.CombatTargets;

        // 随机拨号：三个 0~4 的数字（可重复；费用只有 0/1/2/3/4 和 X，X 费不会被拨号）
        var dial = new int[3];
        for (int i = 0; i < 3; i++)
        {
            dial[i] = rng.NextInt(0, 5);
        }

        foreach (var digit in dial)
        {
            // 手牌满则不抽（避免满手改道弃牌堆）
            if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(owner))
            {
                break;
            }
            // 从抽牌堆定向抽取一张费用消耗 == 拨号数字的牌（X 费卡费用不定，排除）
            var card = drawPile.Cards.FirstOrDefault(c =>
                c != null && !c.EnergyCost.CostsX && c.EnergyCost.Canonical == digit);
            if (card == null)
            {
                continue;
            }
            card.RemoveFromCurrentPile(silent: true);
            await CardPileCmd.Add(card, PileType.Hand);
        }
    }
}
