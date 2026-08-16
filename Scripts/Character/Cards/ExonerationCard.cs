using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 脱罪力证 (Exoneration) - 1费技能牌（罕见，冰霜派系）。
/// 获得1层无实体。不可升级。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ExonerationCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 不可升级
    /// </summary>
    public override int MaxUpgradeLevel => 0;

    /// <summary>
    /// 法术牌 + 冰霜派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Frost];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：脱罪力证（程序绘制：冰霜护盾）
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/exoneration.png";

    public ExonerationCard()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 获得 1 层无实体（游戏原生 IntangiblePower）
        await PowerCmd.Apply<IntangiblePower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
