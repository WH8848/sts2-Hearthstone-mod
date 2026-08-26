using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 落难的大法师 (Marooned Archmage) - 吉安娜专属随从。
/// 属性：攻击 3，生命 4。你每个回合使用的第一张法术牌的费用消耗减少1点。
/// </summary>
[RegisterMonster]
public sealed class MaroonedArchmageMinion : JainaMinionBase
{
    /// <summary>
    /// 本回合玩家已施放的法术数（第一张之后不再减费；每回合开始重置）。
    /// 实例字段：每只各自判断——多只在场时第一张法术可多次减费（光环叠加）。
    /// </summary>
    private int _spellsCastThisTurn;

    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    /// <summary>
    /// 战斗视觉：落难的大法师卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/marooned_archmage.png";

    /// <summary>
    /// 玩家回合开始：重置本回合施放计数
    /// </summary>
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        await base.BeforeSideTurnStart(choiceContext, side, participants, combatState);
        if (side == CombatSide.Player)
        {
            _spellsCastThisTurn = 0;
            if (Creature.PetOwner is { } po)
            {
                // 清零本回合法术计数（与卡雷苟斯同步;幂等）
                jaina.Scripts.Character.JainaCastTracker.ResetTurnAttackSkillCount(po);
            }
        }
    }

    /// <summary>
    /// 你每个回合使用的第一张法术牌的费用消耗减少1点：
    /// 战斗中费用解析时调用——主人（吉安娜）的法术牌且本回合尚未施放过法术时减 1 费。
    /// 随从为本回合中途登场：若本回合第一张法术已在登场前打出，减费窗口已过
    /// （"当前回合第一张法术"——不是"登场后下一张"）。
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!Creature.IsAlive)
        {
            return false;
        }
        if (_spellsCastThisTurn == 0 && Creature.PetOwner is { } po &&
            jaina.Scripts.Character.JainaCastTracker.HasPlayedAttackOrSkillThisTurn(po))
        {
            _spellsCastThisTurn = 1; // 窗口已过:本回合第一张法术已在登场前打出
        }
        if (_spellsCastThisTurn > 0)
        {
            return false;
        }
        var owner = Creature.PetOwner;
        if (owner == null || card.Owner != owner)
        {
            return false;
        }
        // 法术牌 = 攻击/技能牌（不含英雄技能卡）
        if (card.Type != CardType.Attack && card.Type != CardType.Skill)
        {
            return false;
        }
        if (HeroPowerHandHelper.IsHeroPowerCard(card))
        {
            return false;
        }
        modifiedCost = Math.Max(0, originalCost - 1);
        return true;
    }

    /// <summary>
    /// 施放法术（含衍生施放）后计数：第一张之后的法术不再享受减费
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!Creature.IsAlive || Creature.PetOwner == null || cardPlay.Card.Owner != Creature.PetOwner)
        {
            return;
        }
        var card = cardPlay.Card;
        if (card.Type != CardType.Attack && card.Type != CardType.Skill)
        {
            return;
        }
        if (HeroPowerHandHelper.IsHeroPowerCard(card))
        {
            return;
        }
        _spellsCastThisTurn++;
        await Task.CompletedTask;
    }
}
