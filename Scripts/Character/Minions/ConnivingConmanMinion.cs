using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 蓄谋诈骗犯 (Conniving Conman) - 吉安娜专属随从。
/// 属性：攻击 4，生命 4。
/// 战吼：再次使用你使用过的上一张卡牌（重放最近施放的一张攻击/技能牌，
/// 恢复其升级级别与本局衍生状态；单目标牌随机选合法目标）。
/// </summary>
[RegisterMonster]
public sealed class ConnivingConmanMinion : JainaMinionBase
{
    /// <summary>
    /// 战斗视觉：蓄谋诈骗犯卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/conniving_conman.png";

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    /// <summary>
    /// 战吼：再次使用你使用过的上一张卡牌。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        var combatState = owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        // 只重放"我"（打出诈骗犯的玩家）施放的上一张牌——按玩家区分，联机不误用队友的
        if (!rec.LastPlayedCardByPlayer.TryGetValue(owner.NetId, out var last) ||
            last is not { } played)
        {
            return;
        }
        var (type, upgradeLevel, isGenerated) = played;

        // 只可重放法术牌（攻击/技能牌）：
        // 英雄技能卡（火焰冲击/奥术爆裂等）不可被诈骗犯再次使用；
        // 随从卡（部分随从卡带法术关键词也会被记录）不可重放——
        // 重放随从卡只会召唤随从且不触发战吼，与炉石规则不符。
        var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(type));
        if (canonical == null)
        {
            return;
        }
        if (HeroPowerHandHelper.IsHeroPowerCard(canonical))
        {
            return;
        }
        if (typeof(jaina.Scripts.Character.Cards.JainaMinionCardTemplate).IsAssignableFrom(type))
        {
            return;
        }

        // 按记录创建上一张卡的副本（恢复升级级别）
        var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, owner, type, upgradeLevel);
        if (card == null)
        {
            return;
        }
        if (isGenerated)
        {
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
        }

        // 随机目标：AnyEnemy 单体攻击牌除非描述限定"对敌人"，目标放宽为全部存活生物
        // （自己/队友角色、双方随从、敌人）；其余按卡合法性过滤（合法优先，回退全量）。
        var target = jaina.Scripts.Character.JainaRandomPoolHelper.PickRandomTarget(owner, combatState, card);
        if (card.TargetType != TargetType.None && target == null)
        {
            return;
        }
        // 重放的牌添加"消耗"：打出后进入消耗堆（不再进入弃牌堆，避免被反复重放）
        card.AddKeyword(CardKeyword.Exhaust);
        // 标记自动打出（不计入"手打"计数，避免重放自身膨胀）
        jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(card);
        await CardCmd.AutoPlay(choiceContext, card, target);
    }
}
