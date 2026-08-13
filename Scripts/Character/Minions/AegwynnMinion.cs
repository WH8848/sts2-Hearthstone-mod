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
/// 属性：攻击 5，生命 5。力量+2，亡语：你抽到的下一张随从牌会继承此能力。
/// </summary>
[RegisterMonster]
public sealed class AegwynnMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 5;

    public override int MaxInitialHp => 5;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/aegwynn.tscn";

    /// <summary>
    /// 拥有亡语词条
    /// </summary>
    public override bool HasDeathrattle => true;

    /// <summary>
    /// 战吼：力量光环 +2（挂在随从自身，随从在场期间主人攻击 +2，死亡自动消失）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        await PowerCmd.Apply<AegwynnAuraPower>(choiceContext, [Creature], 2m, Creature, options.Source);
    }

    /// <summary>
    /// 亡语：下一张抽到的随从牌继承此能力（+2 力量）
    /// </summary>
    public override async Task OnDeathrattle(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        await PowerCmd.Apply<AegwynnLegacyPower>(choiceContext, [owner.Creature], 1m, Creature, null);
    }
}
