using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 火焰冲击 (Fireblast) - 吉安娜专属卡牌，只出现在初始卡组中。
/// 0费造成1点伤害，可无限升级，每回合开始自动加入手牌。
/// 使用 Basic 稀有度使其不出现战斗奖励掉落中。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterCharacterStarterCard(typeof(Jaina), 1)]
public sealed class Fireblast : ModCardTemplate
{
    /// <summary>
    /// 无限升级 - 允许无限次升级
    /// </summary>
    public override int MaxUpgradeLevel => int.MaxValue;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(1m, ValueProp.Move)
    ];

    /// <summary>
    /// 卡牌原画：炉石传说法师英雄技能"火焰冲击"高清原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/fireblast.png";

    public Fireblast()
        : base(0, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .WithHitFx("vfx/vfx_attack_blunt", null, "blunt_attack.mp3")
            .Execute(choiceContext);
    }

    /// <summary>
    /// 每回合开始自动加入手牌
    /// </summary>
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == base.Owner)
        {
            CardPile? pile = base.Pile;
            if (pile == null || pile.Type != PileType.Hand)
            {
                await CardPileCmd.Add(this, PileType.Hand);
            }
        }
    }

    protected override void OnUpgrade()
    {
        // 每次升级伤害 +1
        base.DynamicVars.Damage.UpgradeValueBy(1m);
    }
}