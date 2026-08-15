using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 幸运币 (The Coin) - 0费技能：获得 1 点能量。[gold]保留[/gold]。
/// 衍生卡：由初始遗物冰霜符文在每场战斗开始时生成，
/// 使用 Token 稀有度 + 中立衍生池，不进入掉落池与商店。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class LuckyCoin : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌（攻击牌和技能牌都视为法术牌）+ 保留 + 消耗：
    /// 保留：回合结束时留在手牌（游戏原生关键词，自动生效并注入卡面描述文本）；
    /// 消耗：打出后从本场战斗移除（游戏原生关键词）。
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, CardKeyword.Retain, CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：炉石传说"幸运币"（The Coin, GAME_005）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/the_coin.png";

    public LuckyCoin()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.None, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 获得 1 点能量
        await PlayerCmd.GainEnergy(1m, base.Owner);
    }
}
