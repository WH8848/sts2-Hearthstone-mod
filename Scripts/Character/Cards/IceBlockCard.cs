using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 寒冰屏障 (Ice Block) - 1费能力牌（稀有，冰霜派系）。
/// 获得 1 层常驻可叠层的寒冰屏障：当你将要承受致命伤害时，移除 1 层，
/// 此次致命伤害变为 0，并获得免疫直到下回合开始。不可升级。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class IceBlockCard : ModCardTemplate
{
    /// <summary>
    /// 不可升级
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>
    /// 法术牌 + 冰霜派系 + 免疫（悬停解释）。视为法术牌（可被复制）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell,
         jaina.Scripts.Character.Keywords.JainaKeywords.Frost,
         jaina.Scripts.Character.Keywords.JainaKeywords.Immune];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/ice_block.png";

    public IceBlockCard()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂寒冰屏障（致命伤害防护 + 本回合免疫，一次性）
        await PowerCmd.Apply<IceBlockPower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
