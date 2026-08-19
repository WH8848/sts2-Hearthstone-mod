using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 灰贤鹦鹉 (Grey Sage Parrot) - 吉安娜随从卡。召唤 4/5 的 GreySageParrotMinion。
/// 战吼：重复你施放的上一个费用消耗大于等于 2 点的法术。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class GreySageParrotCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 野兽种族 + 战吼（悬停解释）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Beast, jaina.Scripts.Character.Keywords.JainaKeywords.Battlecry,
         CardKeyword.Exhaust];

    public override string CustomPortraitPath => "res://assets/card_art/grey_sage_parrot.png";
    protected override Type MinionType => typeof(GreySageParrotMinion);

    protected override int MinionAttack => 4;

    protected override int MinionHealth => 5;

    /// <summary>
    /// 悬停额外提示：战斗中显示"自己施放的上一个费用 ≥ 2 的法术"卡面（动态；
    /// 无记录/非战斗时不显示；按玩家区分，联机只显示自己的）。
    /// 按施放时的升级级别恢复副本——悬停显示的卡面与实际战吼重复的卡一致
    /// （用 canonical 模板卡会永远显示未升级形态，与打出的升级卡不符）。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips
    {
        get
        {
            if (base.Owner?.Creature?.CombatState is { } combatState &&
                jaina.Scripts.Character.JainaCastTracker.For(combatState).LastCastSpellCost2PlusByPlayer.TryGetValue(
                    base.Owner.NetId, out var last) && last is { } played)
            {
                var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                    combatState, base.Owner, played.Type, played.UpgradeLevel);
                if (card != null)
                {
                    yield return new CardHoverTip(card);
                }
            }
        }
    }

    public GreySageParrotCard()
        : base(2, CardRarity.Uncommon)
    {
    }
}
