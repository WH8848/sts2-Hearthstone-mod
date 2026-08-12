using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 卡雷苟斯 (Kalecgos) - 吉安娜专属随从。
/// 属性：攻击 4，生命 12。
/// 注：原效果"每回合第一张攻击/技能牌 0 费 + 战吼发现"——机制复杂，暂为纯属性随从。
/// </summary>
[RegisterMonster]
public sealed class KalecgosMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 12;

    public override int MaxInitialHp => 12;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/kalecgos.tscn";
}
