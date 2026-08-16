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

    protected override string MinionVisualsPath => "res://assets/card_art/sanctum_candle_seller.png";

    /// <summary>
    /// 施放火焰法术后：抽一张法术牌（从抽牌堆中找一张攻击/技能牌入手；没有则普通抽一张）
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!Creature.IsAlive || Creature.PetOwner == null || cardPlay.Card.Owner != Creature.PetOwner)
        {
            return;
        }
        var card = cardPlay.Card;
        // 只统计法术牌（攻击牌和技能牌）且带火焰派系关键词
        if (card.Type != CardType.Attack && card.Type != CardType.Skill)
        {
            return;
        }
        if (!card.Keywords.Contains(JainaKeywords.Fire))
        {
            return;
        }

        // 抽一张法术牌：优先从抽牌堆找攻击/技能牌入手，否则普通抽一张
        var player = Creature.PetOwner;
        var drawPile = player.PlayerCombatState?.DrawPile;
        var spell = drawPile?.Cards.FirstOrDefault(c =>
            c != null && (c.Type == CardType.Attack || c.Type == CardType.Skill));
        if (spell == null)
        {
            await CardPileCmd.Draw(choiceContext, 1, player);
            return;
        }
        if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
        {
            return;
        }
        await CardPileCmd.Add(spell, PileType.Hand);
    }
}
