using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Cards;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 卡雷苟斯 (Kalecgos) - 吉安娜专属随从。
/// 属性：攻击 4，生命 12。
/// 你每个回合使用的第一张攻击牌或技能牌的费用为0点。战吼：发现一张攻击牌或技能牌。
/// </summary>
[RegisterMonster]
public sealed class KalecgosMinion : JainaMinionBase
{
    public override JainaMinionBehaviorMode BehaviorMode => JainaMinionBehaviorMode.Manual;

    public override int MinInitialHp => 12;

    public override int MaxInitialHp => 12;

    protected override string MinionVisualsPath => "res://assets/card_art/kalecgos.png";

    /// <summary>
    /// 召唤时：挂上"第一张攻击/技能牌 0 费"的光环（任何召唤方式都生效）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);

        // 每回合第一张攻击/技能牌 0 费（光环挂随从自身，随从死亡自动失效）
        await PowerCmd.Apply<KalecgosPower>(choiceContext, [Creature], 1m, Creature, options.Source);
    }

    /// <summary>
    /// 战吼：发现一张攻击牌或技能牌。仅手牌打出时触发。
    /// </summary>
    public override async Task OnBattlecry(PlayerChoiceContext choiceContext)
    {
        var owner = Creature.PetOwner;
        if (owner != null)
        {
            await JainaDiscoverHelper.DiscoverAndAddToHand(choiceContext, owner);
        }
    }
}
