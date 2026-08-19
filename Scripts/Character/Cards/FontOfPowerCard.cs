using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Keywords;
using jaina.Scripts.Character.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using MegaCrit.Sts2.Core.HoverTips;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 能量之泉 (Font of Power) - 0费技能牌（普通，奥术派系）。
/// 发现一张法师随从牌。如果你的抽牌堆中没有随从牌，改为获取全部三张牌。
/// 基础版消耗；升级后不再消耗。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class FontOfPowerCard : JainaSpellCardTemplate, Powers.IJainaConditionGlowCard
{
    /// <summary>
    /// 升级后仍发光：升级只移除消耗，"抽牌堆无随从 → 获取全部三张"机制保留
    /// （区别于匣中古神/埃匹希斯冲击/加工失误——升级后条件效果关闭）。
    /// </summary>
    public bool GlowsWhenUpgraded => true;

    /// <summary>
    /// 可升级（升级后去除消耗）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    /// <summary>
    /// 法术牌 + 奥术派系；基础版消耗（升级后不再消耗）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [JainaKeywords.Spell, JainaKeywords.Arcane]
        : [JainaKeywords.Spell, JainaKeywords.Arcane, CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];


    public override string CustomPortraitPath => "res://assets/card_art/font_of_power.png";

    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromKeyword(JainaKeywords.Discover)];

    public FontOfPowerCard()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级：移除消耗（LocalKeywords 懒初始化只算一次，升级形态 Keywords
    /// 缓存自基础状态——需显式移除 Exhaust，否则升级后卡面仍显示"消耗"）。
    /// </summary>
    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
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
        var rng = player.RunState.Rng.CombatTargets;

        // 法师随从牌池：吉安娜卡池中的随从卡（不含英雄技能卡），按可升级级别展开
        var pool = new List<CardModel>();
        foreach (var canonical in ModelDb.CardPool<JainaCardPool>().AllCards)
        {
            if (canonical == null || canonical.Type != JainaCardTypes.Minion)
            {
                continue;
            }
            if (canonical.CanonicalKeywords?.Contains(JainaKeywords.HeroPower) == true)
            {
                continue;
            }
            int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(canonical.GetType());
            for (int level = 0; level <= maxLevel; level++)
            {
                var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                    combatState, player, canonical.GetType(), level);
                if (card != null)
                {
                    pool.Add(card);
                }
            }
        }
        if (pool.Count == 0)
        {
            return;
        }

        // 随机取三张（不足 3 张全给）
        var picked = new List<CardModel>();
        while (picked.Count < 3 && pool.Count > 0)
        {
            var card = rng.NextItem(pool);
            if (card == null)
            {
                break;
            }
            picked.Add(card);
            pool.Remove(card);
        }

        // 抽牌堆中没有随从牌：获取全部三张；否则三选一
        bool hasMinionInDrawPile = player.PlayerCombatState?.DrawPile.Cards.Any(
            c => c != null && c.Type == JainaCardTypes.Minion) ?? false;
        if (!hasMinionInDrawPile)
        {
            foreach (var card in picked)
            {
                if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
                {
                    return;
                }
                jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
            }
            return;
        }

        // 三选一（可跳过）
        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, picked, player, canSkip: true);
        if (chosen == null)
        {
            return;
        }
        if (jaina.Scripts.Character.JainaHandHelper.IsHandFull(player))
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(chosen);
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, player);
    }
}
