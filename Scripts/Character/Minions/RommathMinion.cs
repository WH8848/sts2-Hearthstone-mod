using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 大法师罗曼斯 (Archmage Rommath) - 吉安娜专属随从。
/// 属性：攻击 5，生命 7。
/// 注：原效果"再次施放牌库外的攻击/技能牌（随机目标）"——机制复杂，暂为纯属性随从。
/// </summary>
[RegisterMonster]
public sealed class RommathMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 7;

    public override int MaxInitialHp => 7;

    protected override string MinionVisualsPath => "res://assets/minion_visuals/archmage_rommath.tscn";
}
