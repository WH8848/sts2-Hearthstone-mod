using System.Threading.Tasks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Cards;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 棱光元素 (Prismatic Elemental) - 吉安娜专属随从。
/// 属性：攻击 1，生命 2（元素）。
/// 战吼：发现一张任意角色（全职业）的卡牌，使其费用减少1点（三选一，可跳过；加入手牌）。
/// </summary>
[RegisterMonster]
public sealed class PrismaticElementalMinion : JainaMinionBase
{
    /// <summary>
    /// 战斗视觉：棱光元素卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/prismatic_elemental.png";

    public override int MinInitialHp => 2;

    public override int MaxInitialHp => 2;

    /// <summary>
    /// 战吼：发现一张任意角色（全职业）的卡牌，使其费用减少1点。
    /// 全角色池（ModelDb.AllCards，应用 Jaina 随机池统一排除）；
    /// 同名卡不可自发现（排除棱光元素自身）。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        var ownCardType = JainaMinionCardMap.GetCardType(GetType());
        await JainaDiscoverHelper.DiscoverAllClassesCardAndReduceCostByOne(
            choiceContext, owner, ownCardType);
    }
}
