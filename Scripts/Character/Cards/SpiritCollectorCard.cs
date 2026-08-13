using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using jaina.Scripts.Character.Minions;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 灵体采集者 (Spirit Collector) 随从卡 - 0费召唤一个 2/1 的灵体采集者。
/// 战吼：获取一张 0 费 1/1 的小精灵，并灌注你的英雄技能（+1 英雄技能伤害与一个小精灵）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class SpiritCollectorCard : JainaMinionCardTemplate
{
    protected override Type MinionType => typeof(SpiritCollectorMinion);

    protected override int MinionAttack => 2;

    protected override int MinionHealth => 1;

    public SpiritCollectorCard()
        : base(0, CardRarity.Common)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.OnPlay(choiceContext, cardPlay);

        // 获取一张 0 费 1/1 的小精灵（加入手牌）——MutableClone 无 Owner 会 NRE，用 CreateCard 生成带 Owner 的实例
        var combatState = base.Owner.Creature.CombatState;
        var imp = combatState.CreateCard((MegaCrit.Sts2.Core.Models.CardModel)MegaCrit.Sts2.Core.Models.ModelDb.GetById<ImpCard>(MegaCrit.Sts2.Core.Models.ModelDb.GetId(typeof(ImpCard))), base.Owner);
        await CardPileCmd.AddGeneratedCardToCombat(imp, PileType.Hand, base.Owner);

        // 灌注你的英雄技能（+1 层灌注）
        await PowerCmd.Apply<EmpowerPower>(choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
