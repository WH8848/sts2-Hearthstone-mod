using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Keywords;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 圣殿蜡烛商 (Sanctum Candleseller) - 吉安娜专属随从。
/// 属性：攻击 4，生命 5。在你施放一个火焰法术后，抽一张法术牌。
/// </summary>
[RegisterMonster]
public sealed class SanctumCandlesellerMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 5;

    public override int MaxInitialHp => 5;

    protected override string MinionVisualsPath => "res://assets/card_art/sanctum_chandler.png";

    /// <summary>
    /// 施放火焰法术后：抽一张法术牌——优先从抽牌堆找攻击/技能牌入手，
    /// 抽牌堆没有则从弃牌堆找，都没有则普通抽一张。
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!Creature.IsAlive || Creature.PetOwner == null || cardPlay.Card.Owner != Creature.PetOwner)
        {
            return;
        }
        var card = cardPlay.Card;
        // 只统计法术牌（攻击/技能牌，或带"法术牌"关键词的能力牌）
        if (card.Type != CardType.Attack && card.Type != CardType.Skill &&
            !card.Keywords.Contains(JainaKeywords.Spell))
        {
            return;
        }
        if (!card.Keywords.Contains(JainaKeywords.Fire))
        {
            return;
        }

        var player = Creature.PetOwner;
        if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
        {
            return;
        }
        var combatState = player.PlayerCombatState;
        if (combatState == null)
        {
            return;
        }
        // 优先抽牌堆（法术牌统一判定：攻击/技能，或带"法术牌"关键词的能力牌）
        var drawPile = combatState.DrawPile;
        var spell = drawPile.Cards.FirstOrDefault(c =>
            c != null && jaina.Scripts.Character.JainaCastTracker.IsSpellCard(c));
        if (spell != null)
        {
            await CardPileCmd.Add(spell, PileType.Hand);
            return;
        }
        // 抽牌堆没有：从弃牌堆找
        var discardPile = combatState.DiscardPile;
        var discarded = discardPile.Cards.FirstOrDefault(c =>
            c != null && jaina.Scripts.Character.JainaCastTracker.IsSpellCard(c));
        if (discarded != null)
        {
            await CardPileCmd.Add(discarded, PileType.Hand);
            return;
        }
        // 都没有：普通抽一张
        await CardPileCmd.Draw(choiceContext, 1, player);
    }
}
