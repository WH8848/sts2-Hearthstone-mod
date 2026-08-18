using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.Interop.AutoRegistration;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 蓄谋诈骗犯 (Conniving Conman) - 1费随从卡（罕见）。
/// 战吼：再次使用你使用过的上一张卡牌。属性 4/4。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ConnivingConmanCard : JainaMinionCardTemplate
{
    /// <summary>
    /// 卡牌原画：炉石传说"蓄谋诈骗犯"（Conniving Conman, VAC）官方原画
    /// </summary>
    public override string CustomPortraitPath => "res://assets/card_art/conniving_conman.png";

    protected override Type MinionType => typeof(ConnivingConmanMinion);

    protected override int MinionAttack => 4;

    protected override int MinionHealth => 4;

    /// <summary>
    /// 战吼（悬停解释）+ 消耗（随从卡打出后消耗，模板默认）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Battlecry, CardKeyword.Exhaust];

    /// <summary>
    /// 悬停额外提示：战斗中显示"自己打出的上一张卡"卡面（动态；
    /// 无记录/非战斗时不显示；按玩家区分，联机只显示自己的）。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraMinionHoverTips
    {
        get
        {
            if (base.Owner?.Creature?.CombatState is { } combatState &&
                jaina.Scripts.Character.JainaCastTracker.For(combatState).LastPlayedCardByPlayer.TryGetValue(
                    base.Owner.NetId, out var last) && last is { } played)
            {
                var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(played.Type));
                if (canonical != null)
                {
                    yield return new CardHoverTip(canonical);
                }
            }
        }
    }

    public ConnivingConmanCard()
        : base(1, CardRarity.Uncommon)
    {
    }
}
