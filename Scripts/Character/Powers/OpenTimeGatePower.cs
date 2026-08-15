using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 打开时空之门光环：施放 8 个"你的牌库之外的法术牌"（对局内衍生的攻击/技能牌）
/// 后获得奖励：时空扭曲直接置入手牌，随后本 Power 消失
/// （打出 1 次打开时空之门只能获得 1 次奖励）。
/// 挂在玩家身上，打出打开时空之门时施加。
/// </summary>
[RegisterPower]
public sealed class OpenTimeGatePower : PowerModel
{
    /// <summary>需要施放的牌库之外法术牌数量</summary>
    private const int RequiredCasts = 8;

    /// <summary>奖励的时空扭曲是否为升级版（时空扭曲+）</summary>
    public bool RewardUpgraded { get; set; }

    private int _count;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = Owner?.Player;
        if (player == null || cardPlay.Card.Owner != player)
        {
            return;
        }
        // 只计数"牌库之外的法术牌"（对局内衍生的攻击/技能牌）施放
        var card = cardPlay.Card;
        if (card.Type != CardType.Attack && card.Type != CardType.Skill)
        {
            return;
        }
        if (!jaina.Scripts.Character.JainaCastTracker.IsGeneratedCard(card))
        {
            return;
        }
        _count++;
        if (_count < RequiredCasts)
        {
            return;
        }
        // 达到 8 个：奖励时空扭曲（升级后为时空扭曲+）直接置入手牌
        var canonical = ModelDb.GetByIdOrNull<CardModel>(
            ModelDb.GetId(typeof(jaina.Scripts.Character.Cards.TimeWarpCard)));
        if (canonical == null)
        {
            return;
        }
        if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
        {
            return;
        }
        var combatState = player.Creature.CombatState;
        var warp = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, player, typeof(jaina.Scripts.Character.Cards.TimeWarpCard), RewardUpgraded ? 1 : 0);
        if (warp == null)
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(warp);
        await CardPileCmd.AddGeneratedCardToCombat(warp, PileType.Hand, player);
        await PowerCmd.Remove(this);
    }
}
