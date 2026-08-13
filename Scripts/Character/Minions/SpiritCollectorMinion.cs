using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 灵体采集者 (Spirit Collector) - 吉安娜专属随从。
/// 属性：攻击 2，生命 1。被召唤时：获取一张 0 费 1/1 的小精灵，并灌注你的英雄技能。
/// </summary>
[RegisterMonster]
public sealed class SpiritCollectorMinion : JainaMinionBase
{
    /// <summary>
    /// 手动模式（默认）：不自动攻击，靠点击行动
    /// </summary>
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 1;

    public override int MaxInitialHp => 1;

    /// <summary>
    /// 战斗视觉：灵体采集者卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/spirit_collector.png";
}
