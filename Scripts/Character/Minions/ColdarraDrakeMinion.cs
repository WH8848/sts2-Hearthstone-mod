using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 考达拉幼龙 (Coldarra Drake) - 吉安娜专属随从。
/// 属性：攻击 6，生命 7。
/// 你的英雄技能变为1费，你可以使用任意次数的英雄技能。
/// 光环挂在随从身上：随从死亡自动失效。
/// </summary>
[RegisterMonster]
public sealed class ColdarraDrakeMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 7;

    public override int MaxInitialHp => 7;

    /// <summary>
    /// 战斗视觉：考达拉幼龙卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/coldarra_drake.png";

    /// <summary>
    /// 召唤时：挂"英雄技能1费+任意次数"光环（任何召唤方式都生效，随从死亡自动失效）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);
        await PowerCmd.Apply<ColdarraDrakePower>(choiceContext, [Creature], 1m, Creature, options.Source);
    }
}
