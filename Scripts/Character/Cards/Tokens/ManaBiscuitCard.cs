using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 法力饼干 (Mana Biscuit) - 0费技能（衍生，Token）：复原能量（默认 2 费，
/// 由"制造法力饼干"按基础/升级形态设置为 1/2 费）。
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

    /// <summary>
    /// 复原能量数（1 = 基础形态的制造法力饼干生成；2 = 升级形态生成）
    /// </summary>
    private int _restoreAmount = 2;

    /// <summary>
    /// 复原能量数；写入时同步卡面动态变量 Amount（描述显示复原 {Amount} 费）。
    /// </summary>
    public int RestoreAmount
    {
        get => _restoreAmount;
        set
        {
            _restoreAmount = System.Math.Max(0, value);
            if (DynamicVars.TryGetValue("Amount", out var v))
            {
                v.BaseValue = _restoreAmount;
            }
        }
    }

    protected override IEnumerable<MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar> CanonicalVars =>
        [new MegaCrit.Sts2.Core.Localization.DynamicVars.DynamicVar("Amount", 2m)];

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
        // 复原能量（RestoreAmount 费）：回复能量但不超过能量上限
        var pcs = base.Owner.PlayerCombatState;
        if (pcs == null)
        {
            return;
        }
        var gain = Math.Min((decimal)RestoreAmount, pcs.MaxEnergy - pcs.Energy);
        if (gain > 0)
        {
            await PlayerCmd.GainEnergy(gain, base.Owner);
        }
    }
}
