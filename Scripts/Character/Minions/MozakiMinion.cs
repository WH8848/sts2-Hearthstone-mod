using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 决斗大师莫扎奇 (Mozaki Master of Flame) - 吉安娜专属随从。
/// 属性：攻击 3，生命 8。在你施放一张攻击牌或技能牌后，获得力量+1。
/// 力量为动态 StrengthPower：莫扎奇在场期间打牌 +1（玩家力量图标可见），
/// 随从死亡时把累计的力量全部移除。
/// </summary>
[RegisterMonster]
public sealed class MozakiMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 8;

    public override int MaxInitialHp => 8;

    protected override string MinionVisualsPath => "res://assets/card_art/mozaki.png";

    /// <summary>
    /// 本随从累计给予玩家的力量层数（死亡时全部移除）
    /// </summary>
    private int _grantedStrength;

    /// <summary>
    /// 死亡时触发清理（移除累计力量，卡面无亡语词条）
    /// </summary>
    public override bool HasDeathrattle => true;

    /// <summary>
    /// 施放攻击/技能牌后：玩家力量 +1
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!Creature.IsAlive || cardPlay.Card.Owner != Creature.PetOwner || Creature.PetOwner == null)
        {
            return;
        }
        var type = cardPlay.Card.Type;
        if (type != CardType.Attack && type != CardType.Skill)
        {
            return;
        }
        await PowerCmd.Apply<StrengthPower>(choiceContext, [Creature.PetOwner.Creature], 1m, Creature, null);
        _grantedStrength++;
    }

    /// <summary>
    /// 死亡：移除累计给予玩家的力量
    /// </summary>
    public override async Task OnDeathrattle(PlayerChoiceContext choiceContext)
    {
        if (_grantedStrength > 0 && Creature.PetOwner != null)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, [Creature.PetOwner.Creature], -_grantedStrength, Creature, null);
        }
    }
}
