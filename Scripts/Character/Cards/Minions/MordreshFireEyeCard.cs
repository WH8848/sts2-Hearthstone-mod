using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 火眼莫德雷斯 (Mordresh Fire Eye) - 3费随从卡（稀有，亡灵）。
/// 属性 8/8。战吼：在本局对战中，如果你用你的英雄技能累计造成了10点伤害，
/// 则对随机敌人造成8次10点伤害。
/// 条件触发发光：英雄技能本局累计伤害达到 10 时，手牌中的本卡深白描边发光
/// （提示玩家现在打出可触发战吼，见 <see cref="IJainaConditionGlowCard"/>）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class MordreshFireEyeCard : JainaMinionCardTemplate, IJainaConditionGlowCard
{
    /// <summary>
    /// 卡牌原画：炉石传说"火眼莫德雷斯"（Mordresh Fire Eye, BAR_547）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/mordresh_fire_eye.png";

    protected override Type MinionType => typeof(MordreshFireEyeMinion);

    protected override int MinionAttack => 8;

    protected override int MinionHealth => 8;

    /// <summary>
    /// 战吼（悬停解释）+ 亡灵种族 + 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry,
         jaina.Scripts.Character.Keywords.JainaKeywords.Undead,
         CardKeyword.Exhaust];

    public MordreshFireEyeCard()
        : base(3, CardRarity.Rare)
    {
    }

    /// <summary>
    /// 发光条件：英雄技能本局累计造成伤害 ≥ 10（与战吼触发条件一致）。
    /// 纯本地 UI 判定：HeroPowerDamageDealtByPlayer 两端确定性同步，联机安全。
    /// </summary>
    public bool IsGlowConditionMet(CardModel card, PlayerCombatState pcs)
    {
        var combatState = card.CombatState ?? card.Owner?.Creature?.CombatState;
        if (combatState == null || card.Owner == null)
        {
            return false;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        rec.HeroPowerDamageDealtByPlayer.TryGetValue(card.Owner.NetId, out var heroPowerDamage);
        return heroPowerDamage >= 10;
    }
}
