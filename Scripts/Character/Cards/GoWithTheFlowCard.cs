using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 顺水漂流 (Go with the Flow) - 0费技能牌（罕见，冰霜派系）。
/// 选择一个角色。如果是敌方，给予其1层冻结；如果是友方随从，使其获得力量+1。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class GoWithTheFlowCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 冰霜派系 + 法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Spell, JainaKeywords.Frost];

    public override string CustomPortraitPath => "res://assets/card_art/go_with_the_flow.png";

    public GoWithTheFlowCard()
        : base(0, CardType.Skill, CardRarity.Uncommon, JainaTargetTypes.AnyTargetable, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        Creature? target = cardPlay.Target;
        if (target is not { IsAlive: true })
        {
            return;
        }
        var owner = base.Owner;
        if (owner == null || owner.Creature == null)
        {
            return;
        }
        if (target.Side != owner.Creature.Side)
        {
            // 敌方：给予其 1 层冻结
            await PowerCmd.Apply<FreezePower>(choiceContext, [target], 1m, owner.Creature, this);
        }
        else if (target != owner.Creature)
        {
            // 友方随从（不含吉安娜自己）：使其获得力量+1
            await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(
                choiceContext, [target], 1m, owner.Creature, this);
        }
        // 选择了友方英雄（吉安娜自己）：无效果
    }
}
