using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 非公平游戏 (Unfair Game) - 1费：如果你这个回合没有受到任何伤害，下个回合抽三张牌。
/// 升级后变为加大音量 (Turn Up Volume)：抽三张攻击牌或技能牌。压轴：从中发现一张复制。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class UnfairGame : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/unfair_game.png";

    public UnfairGame()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            // 加大音量：抽三张攻击牌或技能牌
            var drawn = await CardPileCmd.Draw(choiceContext, 3, base.Owner);
            // 压轴：如果刚好消耗完能量，从中发现一张复制
            if (base.Owner.PlayerCombatState is { Energy: <= 0 })
            {
                var hand = base.Owner.PlayerCombatState?.Hand.Cards;
                if (hand != null && hand.Count > 0)
                {
                    var candidates = new List<CardModel>();
                    foreach (var c in hand)
                    {
                        if (c.Type == CardType.Attack || c.Type == CardType.Skill)
                        {
                            candidates.Add((CardModel)c.MutableClone());
                        }
                    }
                    if (candidates.Count > 0)
                    {
                        var chosen = await MegaCrit.Sts2.Core.Commands.CardSelectCmd.FromChooseACardScreen(choiceContext, candidates.AsReadOnly(), base.Owner, canSkip: true);
                        if (chosen != null)
                        {
                            await CardPileCmd.Add(chosen, PileType.Hand);
                        }
                    }
                }
            }
        }
        else
        {
            // 非公平游戏：挂上监听，若本回合未受伤，下回合开始抽三张
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<jaina.Scripts.Character.Powers.UnfairGamePower>(
                choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
        }
    }
}
