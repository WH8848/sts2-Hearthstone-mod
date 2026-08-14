using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
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
    /// <summary>
    /// 战吼：获取一张 0 费 1/1 小精灵并灌注英雄技能
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry, jaina.Scripts.Character.Keywords.JainaKeywords.Empower, CardKeyword.Exhaust];

    /// <summary>
    /// 卡牌原画：炉石传说"灵体采集者"（Spirit Gatherer, EDR_871）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/spirit_collector.png";

    /// <summary>
    /// 悬停提示：显示战吼获取的小精灵衍生物卡（参考冰冷案例/时空提速）
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return new CardHoverTip(MegaCrit.Sts2.Core.Models.ModelDb.Card<ImpCard>());
        }
    }

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
        // 手牌满时不入手（0.111.1 满手时 Add 会把牌静默改道弃牌堆）
        if (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(base.Owner))
        {
            var combatState = base.Owner.Creature.CombatState;
            var canonical = MegaCrit.Sts2.Core.Models.ModelDb.GetByIdOrNull<MegaCrit.Sts2.Core.Models.CardModel>(
                MegaCrit.Sts2.Core.Models.ModelDb.GetId(typeof(ImpCard)));
            if (canonical != null)
            {
                var imp = combatState.CreateCard(canonical, base.Owner);
                jaina.Scripts.Character.JainaCastTracker.MarkGenerated(imp);
                await CardPileCmd.AddGeneratedCardToCombat(imp, PileType.Hand, base.Owner);
            }
        }

        // 灌注你的英雄技能（+1 层灌注）
        await PowerCmd.Apply<EmpowerPower>(choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
