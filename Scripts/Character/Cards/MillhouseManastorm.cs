using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 米尔豪斯·法力风暴 (Millhouse Manastorm) - 吉安娜专属先古能力牌（2 费）。
/// 每当你打出一张法术牌，随机召唤一个费用消耗相同的随从。
/// 升级后费用消耗变为 1。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterDustyTomeCard(typeof(jaina.Scripts.Character.Jaina))]
public sealed class MillhouseManastorm : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌：攻击牌和技能牌都视为法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：炉石传说"米尔豪斯·法力风暴"（Millhouse Manastorm, EX1_323）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/millhouse_manastorm.png";

    public MillhouseManastorm()
        : base(1, CardType.Power, CardRarity.Ancient, TargetType.None, true)
    {
    }

    /// <summary>
    /// 费用：canonical 为 1（升级后各界面一致显示 1 费）；
    /// 未升级通过此钩子显示/结算为 2 费。
    /// </summary>
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (!IsUpgraded)
        {
            modifiedCost = 2m;
            return true;
        }
        modifiedCost = originalCost;
        return false;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        // 挂米尔豪斯光环：之后每打出一张法术牌，随机召唤同费用随从
        await PowerCmd.Apply<MillhousePower>(
            choiceContext, [base.Owner.Creature], 1m, base.Owner.Creature, this);
    }
}
