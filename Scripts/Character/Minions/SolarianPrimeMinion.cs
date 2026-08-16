using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 终极索兰莉安 (Solarian Prime) - 吉安娜专属随从。
/// 属性：攻击 7，生命 7。力量+1（在场期间玩家力量 +1）。
/// 战吼：随机施放五个法师法术（尽可能以敌人为目标）。
/// </summary>
[RegisterMonster]
public sealed class SolarianPrimeMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 7;

    public override int MaxInitialHp => 7;

    protected override string MinionVisualsPath => "res://assets/card_art/solarian_prime.png";

    /// <summary>
    /// 力量+1：常驻效果（任何召唤方式都触发，随从死亡时移除）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);
        await PowerCmd.Apply<StrengthPower>(choiceContext, [owner.Creature], 1m, Creature, options.Source);
    }

    /// <summary>
    /// 死亡：移除 +1 力量（无亡语词条，覆写 AfterDeath 清理）
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
            await PowerCmd.Apply<StrengthPower>(choiceContext, [owner.Creature], -1m, Creature, null);
        }
    }

    /// <summary>
    /// 战吼：随机施放五个法师法术（尽可能以敌人为目标）。仅手牌打出时触发。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner == null)
        {
            return;
        }
        for (int i = 0; i < 5; i++)
        {
            await MageSpellCaster.CastRandomMageSpell(choiceContext, owner, preferEnemies: true);
        }
    }
}
