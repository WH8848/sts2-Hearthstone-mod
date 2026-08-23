using System.Collections.Generic;
using System.Linq;
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
/// 冰霜新星 (Frost Nova) - 1费技能（罕见，冰霜派系）。
/// 给予敌方全体 7 层冻结。升级后费用 1 -> 0。保留消耗（升级不再移除）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class FrostNova : JainaSpellCardTemplate
{
    /// <summary>
    /// 可升级（升级后费用 1 -> 0）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 法术牌 + 冰霜派系 + 冻结（悬停解释）+ 消耗（升级后保留）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell,
         jaina.Scripts.Character.Keywords.JainaKeywords.Frost,
         jaina.Scripts.Character.Keywords.JainaKeywords.Freeze,
         CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：冰霜新星
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/frost_nova.png";

    public FrostNova()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级：费用 1 -> 0（不再移除消耗）
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 给予敌方全体 7 层冻结
        var combatState = base.Owner.Creature.CombatState;
        var enemies = combatState.GetOpponentsOf(base.Owner.Creature)
            .Where(e => e != null && e.IsAlive)
            .ToList();
        foreach (var enemy in enemies)
        {
            await PowerCmd.Apply<FreezePower>(
                choiceContext, [enemy], 7m, base.Owner.Creature, this);
        }
    }
}
