using System.Threading.Tasks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 拾荒清道夫 (Scrappy Scavenger) - 吉安娜专属随从。
/// 属性：攻击 1，生命 1。
/// 战吼：发现一张费用消耗等同于你剩余费用的卡牌。
/// </summary>
[RegisterMonster]
public sealed class ScrappyScavengerMinion : JainaMinionBase
{
    /// <summary>
    /// 战斗视觉：拾荒清道夫卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/scrappy_scavenger.png";

    public override int MinInitialHp => 1;

    public override int MaxInitialHp => 1;

    /// <summary>
    /// 战吼：发现一张费用消耗等同于剩余费用的卡牌（三选一，可跳过；加入手牌）。
    /// 剩余费用 = 打出本随从后的剩余能量（0费随从不消耗能量）。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        var energy = owner.PlayerCombatState?.Energy ?? 0;
        // 同名卡不可自发现：排除拾荒清道夫自身
        var ownCardType = jaina.Scripts.Character.Minions.JainaMinionCardMap.GetCardType(GetType());
        await JainaDiscoverHelper.DiscoverCardOfCostAndAddToHand(choiceContext, owner, energy, ownCardType);
    }
}
