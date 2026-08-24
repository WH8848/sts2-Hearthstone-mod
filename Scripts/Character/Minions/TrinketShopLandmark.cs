using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Powers;
using MinionLib.Minion;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 小玩物小屋 (Knickknack Shack) - 吉安娜地标。
/// 使用效果：抽一张牌。如果你在本回合中使用抽到的这张牌，重新开启本地标。
/// 耐久度 4（每次使用 -1，归零时地标被摧毁）。
/// </summary>
[RegisterMonster]
public sealed class TrinketShopLandmark : JainaLandmarkBase
{
    /// <summary>
    /// 战斗视觉：小玩物小屋卡图原画场景
    /// </summary>
    protected override string MinionVisualsPath => "res://assets/card_art/knickknack_shack.png";

    /// <summary>
    /// 耐久度 4
    /// </summary>
    public override int LandmarkDurability => 4;

    /// <summary>
    /// 被召唤时：额外挂"抽到的牌"追踪 Power（重新开启地标逻辑用）
    /// </summary>
    public override async Task OnSummon(PlayerChoiceContext choiceContext, Player owner, MinionSummonOptions options)
    {
        await base.OnSummon(choiceContext, owner, options);
        await PowerCmd.Apply<TrinketTrackerPower>(choiceContext, Creature, 1m, owner.Creature, null);
    }

    /// <summary>
    /// 使用效果：抽一张牌；本回合内打出抽到的这张牌则重新开启本地标
    /// （打出时由 <see cref="TrinketTrackerPower.AfterCardPlayed"/> 移除冷却）。
    /// </summary>
    public override async Task OnLandmarkEffect(PlayerChoiceContext choiceContext, Creature target)
    {
        var petOwner = Creature.PetOwner;
        if (petOwner == null)
        {
            return;
        }
        var drawn = await CardPileCmd.Draw(choiceContext, 1m, petOwner);
        var tracker = Creature.GetPower<TrinketTrackerPower>();
        if (tracker != null)
        {
            tracker.DrawnCard = drawn.FirstOrDefault();
            var card = tracker.DrawnCard;
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[JainaTrinket] draw=1 card={(card == null ? "null" : $"{card.Id}({card.GetType().Name})")} tracker={(tracker == null ? "null" : "ok")} durability={Creature.GetPower<jaina.Scripts.Character.Powers.LandmarkDurabilityPower>()?.Amount}");
        }
    }

    /// <summary>
    /// 回合结束：清空"抽到的牌"记录（未在本回合打出则不再生效）。
    /// 不调用基类实现（地标不自动攻击、无回合结束被动）。
    /// </summary>
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player)
        {
            return;
        }
        var tracker = Creature.GetPower<TrinketTrackerPower>();
        if (tracker != null)
        {
            tracker.DrawnCard = null;
        }
        await Task.CompletedTask;
    }
}
