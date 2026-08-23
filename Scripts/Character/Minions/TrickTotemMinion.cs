using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 戏法图腾 (Trick Totem) - 吉安娜专属随从。
/// 属性：攻击 0，生命 3。在你的回合结束时，随机施放一个费用消耗
/// 小于或等于1点的全角色卡牌。
/// 效果挂在<b>本随从</b>身上：随从死亡时 Power 随死亡清理自动移除；
/// 多张图腾各自触发一次（等效原"可叠层"语义）。
/// </summary>
[RegisterMonster]
public sealed class TrickTotemMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 3;

    public override int MaxInitialHp => 3;

    /// <summary>
    /// 战斗视觉：戏法图腾卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/trick_totem.png";

    /// <summary>
    /// 召唤后：把"回合结束随机施放"效果挂到本随从身上
    /// （任何召唤方式都生效，随从死亡自动移除）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);
        await PowerCmd.Apply<jaina.Scripts.Character.Powers.TrickTotemPower>(
            choiceContext, Creature, 1m, owner.Creature, options.Source);
    }
}
