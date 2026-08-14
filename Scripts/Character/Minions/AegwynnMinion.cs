using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 守护者艾格文 (Aegwynn the Guardian) - 吉安娜专属随从。
/// 属性：攻击 5，生命 5。力量+2（艾格文在场期间玩家力量 +2，图标可见）；
/// 亡语：你抽到的下一张随从牌会继承此能力（该随从打出后玩家力量 +2）。
/// </summary>
[RegisterMonster]
public sealed class AegwynnMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 5;

    public override int MaxInitialHp => 5;

    protected override string MinionVisualsPath => "res://assets/card_art/aegwynn.png";

    /// <summary>
    /// 拥有亡语词条
    /// </summary>
    public override bool HasDeathrattle => true;

    /// <summary>
    /// 力量+2：常驻效果（非战吼）——任何召唤方式都触发，随从死亡时由亡语移除
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        await PowerCmd.Apply<StrengthPower>(choiceContext, [owner.Creature], 2m, Creature, options.Source);
    }

    /// <summary>
    /// 亡语：移除 +2 力量，下一张抽到的随从牌继承此能力
    /// </summary>
    public override async Task OnDeathrattle(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        await PowerCmd.Apply<StrengthPower>(choiceContext, [owner.Creature], -2m, Creature, null);
        await PowerCmd.Apply<AegwynnLegacyPower>(choiceContext, [owner.Creature], 1m, Creature, null);
    }
}
