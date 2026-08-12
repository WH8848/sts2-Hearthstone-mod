using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 小精灵 (Imp) - 吉安娜专属随从，由灌注/灵体采集者生成。
/// 属性：攻击 1，生命 1，无特殊效果。
/// </summary>
[RegisterMonster]
public sealed class ImpMinion : JainaMinionBase
{
    /// <summary>
    /// 手动模式（默认）：不自动攻击，靠点击行动
    /// </summary>
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 1;

    public override int MaxInitialHp => 1;

    /// <summary>
    /// 战斗视觉：小精灵卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/minion_visuals/imp.tscn";

    /// <summary>
    /// 小精灵无亡语
    /// </summary>
    public override bool HasDeathrattle => false;

    public override Task OnDeathrattle(PlayerChoiceContext choiceContext) => Task.CompletedTask;
}
