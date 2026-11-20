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
/// 奥术工匠光环：打出法术牌时，获得其实际耗能三倍的格挡。
/// - 带法术牌关键词的牌和状态牌触发：英雄技能不是法术牌，不触发；
/// - <b>随机打出（自动施放：匣中古神/惊奇卡牌/戏法图腾/魔法智慧之球/重放等）不触发</b>。
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
        if (cardPlay.Card.Type != CardType.Status && !cardPlay.Card.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Spell))
        {
            return;
        }
        // 随机打出（自动施放）不触发：调用栈检测 + 实例标记双保险
        if (AutoPlayGuard.IsAutoPlayContext(cardPlay.Card))
        {
            return;
        }
        var energySpent = cardPlay.Resources.EnergySpent;
        if (energySpent > 0)
        {
            await CreatureCmd.GainBlock(owner.Creature, 3m * energySpent, ValueProp.Move, null);
        }
    }
}
