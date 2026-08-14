using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 冰霜新星 (Frost Nova) - 1费技能（罕见，冰霜派系）。
/// 给予敌方全体 4 层冻结。
/// 升级后变为"脱罪力证 (Exoneration)"：获得 1 层无实体。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class FrostNova : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 冰霜派系；未升级还有"冻结"关键词（悬停解释），
    /// 升级后（脱罪力证）为"无实体"，由 AdditionalHoverTips 提供 FromPower 解释。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded
            ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Frost]
            : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Frost, jaina.Scripts.Character.Keywords.JainaKeywords.Freeze];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 悬停提示：基础关键词解释 + 升级后（脱罪力证）追加"无实体"Power 解释
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            foreach (var tip in base.AdditionalHoverTips)
            {
                yield return tip;
            }
            if (IsUpgraded)
            {
                yield return HoverTipFactory.FromPower<IntangiblePower>();
            }
        }
    }

    /// <summary>
    /// 卡牌原画：冰霜新星 / 升级后（脱罪力证）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/exoneration.png" : "res://assets/card_art/frost_nova.png";

    public FrostNova()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"脱罪力证 (Exoneration)"
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        if (IsUpgraded)
        {
            // 脱罪力证：获得 1 层无实体（游戏原生 IntangiblePower）
            await PowerCmd.Apply<IntangiblePower>(
                choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
            return;
        }

        // 冰霜新星：给予敌方全体 4 层冻结
        var combatState = base.Owner.Creature.CombatState;
        var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
            .Where(e => e != null && e.IsAlive)
            .ToList();
        foreach (var enemy in enemies)
        {
            await PowerCmd.Apply<FreezePower>(
                choiceContext, [enemy], 4m, base.Owner.Creature, this);
        }
    }
}
