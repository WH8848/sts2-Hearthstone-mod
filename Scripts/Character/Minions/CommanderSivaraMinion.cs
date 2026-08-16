using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 指挥官西瓦拉 (Commander Sivara) - 吉安娜专属随从。
/// 属性：攻击 3，生命 5。
/// 战吼效果（复制手牌期间施放的法术）在卡牌层 CommanderSivaraCard 中实现（打出卡牌时触发）。
/// </summary>
[RegisterMonster]
public sealed class CommanderSivaraMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 5;

    public override int MaxInitialHp => 5;

    /// <summary>
    /// 战斗视觉：指挥官西瓦拉卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/commander_sivara.png";
}
