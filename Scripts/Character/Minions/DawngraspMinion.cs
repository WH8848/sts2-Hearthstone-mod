using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 奥术师晨拥 (Arcanist Dawngrasp) - 吉安娜专属随从。
/// 属性：攻击 8，生命 8。战吼：力量+3（玩家获得 +3 力量，一次性永久加成）。
/// 由"抵达传送大厅"任务奖励入手。
/// </summary>
[RegisterMonster]
public sealed class DawngraspMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 8;

    public override int MaxInitialHp => 8;

    protected override string MinionVisualsPath => "res://assets/card_art/arcanist_dawngrasp.png";

    /// <summary>
    /// 战吼：力量+3（玩家 +3 力量，一次性永久加成，不随随从死亡移除）。
    /// 仅从手牌打出时触发。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        await PowerCmd.Apply<StrengthPower>(choiceContext, [owner.Creature], 3m, Creature, null);
    }
}
