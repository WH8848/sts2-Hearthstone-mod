using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 变形卷轴 (Shifting Scroll) - 0费技能牌（普通，奥术派系）。
/// 如果这张牌在你的手牌中，每个回合开始时都会变成一张随机法师法术牌。
/// 升级后：获得「保留」，且变形后的牌也具有「保留」。
///
/// 变形时机由 <see cref="ShiftingScrollTurnStartPatch"/> 包装 Hook.BeforeSideTurnStart 驱动
/// （CardModel 本身没有"回合开始"钩子）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ShiftingScrollCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 奥术派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Spell, JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/shifting_scroll.png";

    public ShiftingScrollCard()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.None, true)
    {
    }

    protected override void OnUpgrade()
    {
        // 升级：获得「保留」（变形后的牌也保留，见 TransformNow）
        AddKeyword(CardKeyword.Retain);
    }

    /// <summary>
    /// 把手牌中的一张变形卷轴变形为随机法师法术牌。
    /// 升级版变形结果同样获得「保留」。
    /// </summary>
    public static async Task TransformNow(CardModel card, Player owner)
    {
        var combatState = owner?.Creature?.CombatState;
        if (card == null || owner == null || combatState == null)
        {
            return;
        }

        // 随机法师法术牌池（吉安娜池，含升级形态；已排除先古/英雄技能/任务）
        var pool = JainaCastTracker.BuildAllSpellPool();
        pool.RemoveAll(p => p.Type == typeof(ShiftingScrollCard));
        if (pool.Count == 0)
        {
            return;
        }

        var (type, level) = owner.RunState.Rng.CombatCardSelection.NextItem(pool);
        var replacement = JainaCastTracker.CreateCardWithUpgrade(combatState, owner, type, level);
        if (replacement == null)
        {
            return;
        }

        if (card.IsUpgraded)
        {
            replacement.AddKeyword(CardKeyword.Retain);
        }
        // 变形产物带"变形标记"：下个回合开始会继续变形
        replacement.AddKeyword(JainaKeywords.ShiftingMark);
        JainaCastTracker.MarkGenerated(replacement);
        await CardCmd.Transform(card, replacement);
    }
}
