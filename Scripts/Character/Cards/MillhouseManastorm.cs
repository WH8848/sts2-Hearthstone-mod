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
/// 米尔牢斯·法力风暴 (Jailhouse Manastorm) - 吉安娜专属先古能力牌（2 费）。
/// 每当你打出一张法术牌，随机召唤一个费用消耗相同的随从。
/// 升级后费用保持 2 费，并获得[gold]固有[/gold]（战斗开始时在手牌）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
[RegisterDustyTomeCard(typeof(jaina.Scripts.Character.Jaina))]
public sealed class MillhouseManastorm : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌；升级后额外获得固有（战斗开始时在手牌）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, CardKeyword.Innate]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：炉石传说"监狱豪斯·法力风暴"（Jailhouse Manastorm）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/jailhouse_manastorm.png";

    public MillhouseManastorm()
        : base(2, CardType.Power, CardRarity.Ancient, TargetType.None, true)
    {
    }

    protected override void OnUpgrade()
    {
        // 升级后费用保持 2 费（不降费），仅获得"固有"关键词（见 CanonicalKeywords）
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
