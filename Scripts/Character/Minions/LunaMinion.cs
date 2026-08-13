using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

    /// <summary>
    /// 观星者露娜 (Stargazer Luna) - 吉安娜专属随从。
    /// 属性：攻击 2，生命 4。在你使用最右边的一张手牌后，抽一张牌。
    /// </summary>
    [RegisterMonster]
    public sealed class LunaMinion : JainaMinionBase
    {
        public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

        public override int MinInitialHp => 4;

        public override int MaxInitialHp => 4;

        protected override string MinionVisualsPath => "res://assets/card_art/stargazer_luna.png";

        /// <summary>
        /// 记录本回合打出的牌是否为手牌最右边（打出前判定）
        /// </summary>
        private bool _playedRightmost;

        /// <summary>
        /// 打出前：判定该牌是否为手牌最右边的一张（此时牌还在手牌中）
        /// </summary>
        public override Task BeforeCardPlayed(CardPlay cardPlay)
        {
            _playedRightmost = false;
            if (!Creature.IsAlive || cardPlay.Card.Owner != Creature.PetOwner || Creature.PetOwner == null)
            {
                return Task.CompletedTask;
            }
            var hand = Creature.PetOwner.PlayerCombatState?.Hand?.Cards;
            if (hand is { Count: > 0 })
            {
                _playedRightmost = ReferenceEquals(hand[^1], cardPlay.Card);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 使用最右边的手牌后抽一张牌（光环：仅露娜在场时生效）
        /// </summary>
        public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        {
            if (!_playedRightmost || !Creature.IsAlive)
            {
                return;
            }
            _playedRightmost = false;
            await CardPileCmd.Draw(choiceContext, 1, Creature.PetOwner!);
        }
    }
