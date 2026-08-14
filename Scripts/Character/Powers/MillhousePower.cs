using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 米尔豪斯光环：每当你打出一张法术牌，随机召唤一个费用消耗相同的随从。
/// 挂在玩家身上（随从生物随从牌打出后保留效果，不随随从死亡消失）。
/// </summary>
[RegisterPower]
public sealed class MillhousePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner?.Player == null || cardPlay.Card.Owner != Owner.Player)
        {
            return;
        }
        // 仅法术牌触发（挂"法术牌"关键词；英雄技能不是法术牌）
        if (!cardPlay.Card.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Spell))
        {
            return;
        }
        // 按法术实际消耗召唤同费用随从（GetResolved 含降费/零费修正）
        var cost = cardPlay.Card.EnergyCost.GetResolved();
        await JainaMinionPool.SummonRandomMinionOfCost(choiceContext, Owner.Player, cost);
    }
}
