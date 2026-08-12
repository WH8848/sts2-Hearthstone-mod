using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 雷纳索尔王子 (Prince Renathal) - 吉安娜专属随从。
/// 属性：攻击 3，生命 4。
/// 注：原效果"你的套牌容量和初始生命值为40"——游戏无套牌容量机制，暂为纯属性随从。
/// </summary>
[RegisterMonster]
public sealed class RenathalMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/prince_renathal.tscn";
}
