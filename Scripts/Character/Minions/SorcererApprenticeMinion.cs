using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 巫师学徒 (Sorcerer's Apprentice) - 吉安娜专属随从。
/// 属性：攻击 3，生命 2。
/// 你每打出四张攻击牌或技能牌，下一张攻击牌或技能牌消耗减少1点。
/// 光环效果：挂在随从自身，随从死亡后被动失效。
/// </summary>
[RegisterMonster]
public sealed class SorcererApprenticeMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 2;

    public override int MaxInitialHp => 2;

    protected override string MinionVisualsPath => "res://assets/card_art/sorcerer_apprentice.png";

    /// <summary>
    /// 召唤时挂上减费光环
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        await PowerCmd.Apply<SorcererApprenticePower>(choiceContext, [Creature], 1m, Creature, options.Source);
    }
}
