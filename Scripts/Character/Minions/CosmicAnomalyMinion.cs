using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 宇宙异象 (Cosmic Anomaly) - 吉安娜专属随从。
/// 属性：攻击 4，生命 3。力量+2（在场期间玩家力量 +2）。
/// </summary>
[RegisterMonster]
public sealed class CosmicAnomalyMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 3;

    public override int MaxInitialHp => 3;

    /// <summary>
    /// 战斗视觉：宇宙异象卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/cosmic_anomaly.png";

    /// <summary>
    /// 力量+2：常驻效果（任何召唤方式都触发，随从死亡时移除）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);
        await PowerCmd.Apply<StrengthPower>(choiceContext, [owner.Creature], 2m, Creature, options.Source);
    }

    /// <summary>
    /// 死亡：移除 +2 力量（无亡语词条，覆写 AfterDeath 清理）
    /// </summary>
    public override async Task AfterDeath(PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Creatures.Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        await base.AfterDeath(choiceContext, creature, wasRemovalPrevented, deathAnimLength);
        if (creature != Creature)
        {
            return;
        }
        var owner = Creature.PetOwner;
        if (owner != null)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, [owner.Creature], -2m, Creature, null);
        }
    }
}
