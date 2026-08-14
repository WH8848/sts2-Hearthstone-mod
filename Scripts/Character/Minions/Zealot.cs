using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 狂热者 (Zealot) - 吉安娜专属随从。
/// 属性：攻击 3，生命 4。
/// 冲锋：召唤当回合即可点击攻击（炉石语义，非自动攻击）。回合结束攻击。
/// </summary>
[RegisterMonster]
public sealed class Zealot : JainaMinionBase
{
    /// <summary>
    /// 手动模式：玩家可点击攻击（有行动点）
    /// </summary>
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    /// <summary>
    /// 冲锋：召唤当回合即可攻击（召唤时立即授予行动点）
    /// </summary>
    public override bool HasCharge => true;

    /// <summary>
    /// 战斗视觉：狂热者卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/zealot.png";

    public override int MinInitialHp => 4;

    public override int MaxInitialHp => 4;

    /// <summary>
    /// 回合结束被动：攻击随机敌人（保留"回合结束攻击"特性）
    /// </summary>
    protected override Task PerformTurnEndPassive(PlayerChoiceContext choiceContext) =>
        PerformTurnEndAttack(choiceContext);
}
