using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 寒冰行者 (Ice Walker) - 吉安娜专属随从。
/// 属性：攻击 1，生命 3。元素种族。
/// 你的英雄技能还会给与目标 1 层冻结（英雄技能卡打出后冻结目标）。
/// </summary>
[RegisterMonster]
public sealed class IceWalkerMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 3;

    public override int MaxInitialHp => 3;

    protected override string MinionVisualsPath => "res://assets/card_art/ice_walker.png";

    /// <summary>
    /// 召唤时：挂"英雄技能冻结目标"光环（任何召唤方式都生效，随从死亡自动失效）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);
        await PowerCmd.Apply<IceWalkerPower>(choiceContext, [Creature], 1m, Creature, options.Source);
    }
}
