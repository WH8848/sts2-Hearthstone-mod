using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 决斗大师莫扎奇 (Mozaki Master of Flame) - 吉安娜专属随从。
/// 属性：攻击 3，生命 8。在你施放一张攻击牌或技能牌后，获得力量+1。
/// 力量为光环效果：挂在莫扎奇自身，随从死亡后累计的力量全部消失。
/// </summary>
[RegisterMonster]
public sealed class MozakiMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 8;

    public override int MaxInitialHp => 8;

    protected override string MinionVisualsPath => "res://assets/card_art/mozaki.png";

    /// <summary>
    /// 召唤时挂上力量光环（初始 0 层）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        await PowerCmd.Apply<MozakiPower>(choiceContext, [Creature], 0m, Creature, options.Source);
    }

    /// <summary>
    /// 施放攻击/技能牌后：莫扎奇的光环力量 +1
    /// </summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (!Creature.IsAlive || cardPlay.Card.Owner != Creature.PetOwner)
        {
            return;
        }
        var type = cardPlay.Card.Type;
        if (type != CardType.Attack && type != CardType.Skill)
        {
            return;
        }
        var aura = Creature.GetPower<MozakiPower>();
        if (aura != null)
        {
            await PowerCmd.ModifyAmount(choiceContext, aura, 1m, Creature, null);
        }
    }
}
