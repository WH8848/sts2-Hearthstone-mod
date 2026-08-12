using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 巫师学徒 (Sorcerer's Apprentice) - 吉安娜专属随从。
/// 属性：攻击 3，生命 2。
/// 注：原效果"每打出四张攻击/技能牌，下一张费用-1（待定）"——待定，暂为纯属性随从。
/// </summary>
[RegisterMonster]
public sealed class SorcererApprenticeMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 2;

    public override int MaxInitialHp => 2;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/sorcerer_apprentice.tscn";
}
