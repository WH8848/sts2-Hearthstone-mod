using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 守护者艾格文 (Aegwynn the Guardian) - 吉安娜专属随从。
/// 属性：攻击 5，生命 5。
/// 注：原效果"力量+2，亡语：你抽到的下一张随从牌会继承此能力"——暂为纯属性随从。
/// </summary>
[RegisterMonster]
public sealed class AegwynnMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 5;

    public override int MaxInitialHp => 5;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/aegwynn.tscn";
}
