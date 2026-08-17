using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 唤醒 (Evocation) - 0费技能牌（稀有，奥术派系）。
/// 用随机法师法术牌填满你的手牌。这些牌具有虚无。
/// 基础版消耗；升级后不再消耗。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class EvocationCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 奥术派系；基础版消耗（升级后不再消耗）
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane,
           CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 可升级（升级后去除消耗）
    /// </summary>
    public override int MaxUpgradeLevel => 1;

    public override string CustomPortraitPath => "res://assets/card_art/evocation.png";

    public EvocationCard()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.None, true)
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

    /// <summary>
    /// 吉安娜法师法术牌池（与匣中古神随机施放池一致，排除英雄技能卡）
    /// </summary>
    private static readonly System.Type[] MageSpellPool =
    [
        typeof(Fireball),
        typeof(Frostbolt),
        typeof(ArcaneIntellect),
        typeof(FreezingPotion),
        typeof(IceBarrier),
        typeof(Trick),
        typeof(Awaken),
        typeof(NorgannonWisdom),
        typeof(DeepFreezeCard),
        typeof(FlameWard),
        typeof(DeathborneCard),
        typeof(FrostNova),
        typeof(ArcaneBarrage),
        typeof(ApexisBlast),
        typeof(IgniteCard)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rng = base.Owner.RunState.Rng.CombatCardSelection;

        // 用随机法师法术牌填满手牌（不占手牌位的英雄技能卡不影响容量）。
        // 每种法术牌按可升级级别展开：未升级形态与升级形态（+）都可能被填入手牌。
        var pool = new List<(System.Type Type, int UpgradeLevel)>();
        foreach (var t in MageSpellPool)
        {
            var canonical = MegaCrit.Sts2.Core.Models.ModelDb.GetByIdOrNull<MegaCrit.Sts2.Core.Models.CardModel>(
                MegaCrit.Sts2.Core.Models.ModelDb.GetId(t));
            if (canonical == null)
            {
                continue;
            }
            int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(t);
            for (int level = 0; level <= maxLevel; level++)
            {
                pool.Add((t, level));
            }
        }

        while (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(base.Owner))
        {
            if (pool.Count == 0)
            {
                break;
            }
            var entry = rng.NextItem(pool);
            if (entry.Type == null)
            {
                break;
            }
            var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, base.Owner, entry.Type, entry.UpgradeLevel);
            if (card == null)
            {
                continue;
            }
            // 这些牌具有虚无（回合结束时留在手牌则消耗）
            MegaCrit.Sts2.Core.Commands.CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, base.Owner);
        }
    }
}
