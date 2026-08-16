using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Cards;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 潮汐之池 (Tide Pools) - 吉安娜地标。
/// 使用效果：发现一张费用消耗小于或等于1点的法术牌（加入手牌）。
/// 被动：在你施放一个法术后，重新开启本地标（移除冷却，下一回合仍可使用）。
/// 耐久度 3（每次使用 -1，归零时地标被摧毁）。
/// </summary>
[RegisterMonster]
public sealed class TidePoolLandmark : JainaLandmarkBase
{
    /// <summary>
    /// 战斗视觉：潮汐之池卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/tide_pools.png";

    /// <summary>
    /// 耐久度 3
    /// </summary>
    public override int LandmarkDurability => 3;

    /// <summary>
    /// 被召唤时：额外挂"施放法术后重新开启"追踪 Power
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);
        await PowerCmd.Apply<TidePoolTrackerPower>(choiceContext, Creature, 1m, owner.Creature, null);
    }

    /// <summary>
    /// 使用效果：发现一张费用消耗小于或等于1点的法术牌（三选一，可跳过）。
    /// </summary>
    public override async Task OnLandmarkEffect(PlayerChoiceContext choiceContext, Creature target)
    {
        var petOwner = Creature.PetOwner;
        if (petOwner == null)
        {
            return;
        }
        await JainaDiscoverHelper.DiscoverAndAddToHand(choiceContext, petOwner, count: 3, maxCost: 1);
    }
}
