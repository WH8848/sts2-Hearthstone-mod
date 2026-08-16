using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 吉安娜的礼物 (Jaina's Gift) - 0费技能牌（罕见，奥术派系）。
/// 发现一张带有虚无的寒冰箭、奥术智慧或火球术（虚无：回合结束时留在手牌则消耗）。
/// 升级后为倒带 (Rewind)：发现一张你在本局对战中施放过的其他攻击牌或技能牌的一张复制。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class JainasGiftCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 奥术派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌原画：吉安娜的礼物 / 升级后（倒带 Rewind）切换原画
    /// </summary>
    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/rewind.png" : "res://assets/card_art/jainas_gift.png";

    public JainasGiftCard()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"倒带"
    /// </summary>
    public override string Title
    {
        get
        {
            var title = new LocString("cards", base.Id.Entry + ".title");
            if (!IsUpgraded)
            {
                return title.GetFormattedText();
            }
            LocString? upgraded = LocString.GetIfExists("cards", base.Id.Entry + ".titleUpgraded");
            return upgraded?.GetFormattedText() ?? title.GetFormattedText() + "+";
        }
    }

    /// <summary>
    /// 悬停提示：显示未升级时发现候选的三张卡（寒冰箭/奥术智慧/火球术，
    /// 都带虚无），参考灵体采集者显示小精灵的做法。
    /// 升级后（倒带）不显示候选卡。
    /// </summary>
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            if (IsUpgraded)
            {
                yield break;
            }
            yield return new CardHoverTip(ModelDb.Card<Frostbolt>());
            yield return new CardHoverTip(ModelDb.Card<ArcaneIntellect>());
            yield return new CardHoverTip(ModelDb.Card<Fireball>());
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        if (IsUpgraded)
        {
            // 升级后为倒带：发现一张本局施放过的其他攻击牌或技能牌的复制
            await PlayAsRewind(choiceContext);
        }
        else
        {
            // 发现一张带有虚无的寒冰箭、奥术智慧或火球术
            // （每种按可升级级别展开：未升级形态与升级形态（+）都可被发现）
            var combatState = base.Owner.Creature.CombatState;
            if (combatState == null)
            {
                return;
            }
            var pool = new List<CardModel>
            {
                CreateGiftCard(combatState, typeof(Frostbolt), 0),
                CreateGiftCard(combatState, typeof(Frostbolt), 1),
                CreateGiftCard(combatState, typeof(ArcaneIntellect), 0),
                CreateGiftCard(combatState, typeof(ArcaneIntellect), 1),
                CreateGiftCard(combatState, typeof(Fireball), 0),
                CreateGiftCard(combatState, typeof(Fireball), 1)
            };
            pool.RemoveAll(c => c == null);

            var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, pool.AsReadOnly(), base.Owner, canSkip: true);
            if (chosen != null)
            {
                jaina.Scripts.Character.JainaCastTracker.MarkGenerated(chosen);
                if (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(base.Owner))
                {
                    await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, base.Owner);
                }
            }
        }
    }

    /// <summary>
    /// 创建带虚无关键词的礼物候选卡（寒冰箭/奥术智慧/火球术，按升级级别恢复形态）
    /// </summary>
    private CardModel? CreateGiftCard(ICombatState combatState, System.Type cardType, int upgradeLevel)
    {
        var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(cardType));
        if (canonical == null)
        {
            return null;
        }
        var card = combatState.CreateCard(canonical, base.Owner);
        for (int i = 0; i < upgradeLevel && card.CurrentUpgradeLevel < card.MaxUpgradeLevel; i++)
        {
            card.UpgradeInternal();
        }
        // 附加虚无：回合结束时留在手牌则消耗
        CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);
        return card;
    }

    /// <summary>
    /// 升级形态（倒带）：发现一张本局施放过的其他攻击牌或技能牌的一张复制。
    /// 与 Rewind 逻辑一致（排除自身与英雄技能）。
    /// </summary>
    private async Task PlayAsRewind(PlayerChoiceContext choiceContext)
    {
        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        var playedTypes = rec.PlayedAttackSkills
            .Where(t => t != typeof(JainasGiftCard) && t != typeof(Fireblast) && t != typeof(ArcaneBurstCard))
            .ToList();
        if (playedTypes.Count == 0)
        {
            return;
        }

        var rng = base.Owner.RunState.Rng.CombatTargets;
        var pool = new List<System.Type>(playedTypes);
        var candidates = new List<CardModel>();
        while (candidates.Count < 3 && pool.Count > 0)
        {
            var type = rng.NextItem(pool);
            if (type == null)
            {
                break;
            }
            pool.Remove(type);
            rec.PlayedUpgradeLevels.TryGetValue(type, out var upgradeLevel);
            var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                combatState, base.Owner, type, upgradeLevel);
            if (card != null)
            {
                candidates.Add(card);
            }
        }
        if (candidates.Count == 0)
        {
            return;
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, candidates.AsReadOnly(), base.Owner, canSkip: true);
        if (chosen != null)
        {
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(chosen);
            if (!jaina.Scripts.Character.JainaHandHelper.IsHandFull(base.Owner))
            {
                await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, base.Owner);
            }
        }
    }
}
