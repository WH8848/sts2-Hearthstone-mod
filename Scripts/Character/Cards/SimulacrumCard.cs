using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Keywords;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 模拟幻影 (Simulacrum) - 1费技能牌（普通，冰霜派系）。
/// 复制你手牌中法力值消耗最低的随从牌。
/// 升级后费用变为 0。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class SimulacrumCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 可升级（升级后费用 1 -> 0）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 法术牌 + 冰霜派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [JainaKeywords.Spell, JainaKeywords.Frost];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/simulacrum.png";

    public SimulacrumCard()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级：费用 1 -> 0
    /// </summary>
    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var player = base.Owner;
        var combatState = player.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        // 手牌满时 AddGeneratedCardToCombat 自动改道弃牌堆（原版满手语义，牌不消失不消耗）

        // 手牌中法力值消耗最低的随从牌（不含英雄技能卡；按<b>当前费用</b>——GetResolved
        // 含临时减费（巫师学徒/咒术洪流等），不是原始费用——炉石"当前费用最低"语义）
        var hand = PileType.Hand.GetPile(player);
        var cheapest = hand?.Cards
            .Where(c => c != null &&
                        c.Type == JainaCardTypes.Minion &&
                        !jaina.Scripts.Character.Powers.HeroPowerHandHelper.IsHeroPowerCard(c))
            .OrderBy(c => c.EnergyCost.GetResolved())
            .FirstOrDefault();
        if (cheapest == null)
        {
            return;
        }

        // 复制（保留升级级别）
        var copy = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
            combatState, player, cheapest.GetType(), cheapest.CurrentUpgradeLevel);
        if (copy == null)
        {
            return;
        }

        // 艾格文亡语继承：被标记的随从牌被复制时,复制品获得<b>独立</b>的一次继承机会
        // （AegwynnLegacyCopyPower,同层数）——与原卡互不消耗,各自可兑现一次。
        if (player.Creature.GetPower<jaina.Scripts.Character.Powers.AegwynnLegacyPower>() is { } legacy &&
            legacy.IsClaimedCard(cheapest))
        {
            await legacy.ClaimCopyAsync(choiceContext, cheapest, copy);
        }

        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(copy);
        await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, player);
    }
}
