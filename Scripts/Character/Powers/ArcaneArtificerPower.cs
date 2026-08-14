using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 奥术工匠光环：每当你打出一张法术牌，获得等同于其实际消耗能量的护甲值。
/// - 按实际消耗（EnergyCost.GetResolved，含降费/零费修正）：零费打出不叠护甲；
/// - 仅"法术牌"（挂法术牌关键词的牌）触发：英雄技能不是法术牌，不触发。
/// 挂在随从生物自身——随从死亡时本 Power 随生物移除，被动自动失效。
/// </summary>
[RegisterPower]
public sealed class ArcaneArtificerPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => false;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner?.PetOwner;
        if (owner == null || cardPlay.Card.Owner != owner)
        {
            return;
        }
        // 仅法术牌触发（火焰冲击等英雄技能只挂"英雄技能"关键词，不算法术牌）
        if (!cardPlay.Card.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Spell))
        {
            return;
        }
        // 按实际消耗能量叠护甲（GetResolved = 打出后的当前费用，含全部修饰符；零费打出为 0）
        var cost = cardPlay.Card.EnergyCost.GetResolved();
        if (cost > 0)
        {
            await CreatureCmd.GainBlock(owner.Creature, cost, ValueProp.Move, null);
        }
    }
}
