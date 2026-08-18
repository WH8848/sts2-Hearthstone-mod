using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 法力饼干 (Mana Biscuit) - 0费技能（衍生，Token）：复原两费。
/// 衍生卡：由"制造法力饼干"生成，使用 Token 稀有度 + 中立衍生池，不进入掉落池与商店。
/// </summary>
[RegisterCard(typeof(JainaNeutralCardPool))]
public sealed class ManaBiscuitCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 消耗（打出后从本场战斗移除）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, CardKeyword.Exhaust];

    protected override IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：炉石传说"法力饼干"（Mana Biscuit, YOP_019t）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/mana_biscuit.png";

    public ManaBiscuitCard()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.None, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 复原两费：回复能量但不超过能量上限（不能突破上限回复能量）
        var pcs = base.Owner.PlayerCombatState;
        if (pcs == null)
        {
            return;
        }
        var gain = Math.Min(2m, pcs.MaxEnergy - pcs.Energy);
        if (gain > 0)
        {
            await PlayerCmd.GainEnergy(gain, base.Owner);
        }
    }
}
