using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 奥数工匠 (Arcane Artificer) - 吉安娜专属随从。
/// 属性：攻击 1，生命 3。每当你打出一张攻击牌或技能牌，便获得等同于其费用的护甲值。
/// 光环效果：挂在随从自身，随从死亡后被动失效。
/// </summary>
[RegisterMonster]
public sealed class ArcaneArtificerMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 3;

    public override int MaxInitialHp => 3;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/arcane_artificer.tscn";

    /// <summary>
    /// 召唤时挂上护甲光环
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        await PowerCmd.Apply<ArcaneArtificerPower>(choiceContext, [Creature], 1m, Creature, options.Source);
    }
}
