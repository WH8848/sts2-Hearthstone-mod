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
    /// 法术牌：基础版（吉安娜的礼物）无派系；升级版（倒带）为奥术派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded
        ? [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Arcane]
        : [jaina.Scripts.Character.Keywords.JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 该卡类型是否为英雄技能卡（带 HeroPower 关键词：
    /// 火焰冲击/奥术冲击/寒冰之触/远古火焰冲击等）。
    /// 用 canonical 模板判定，与 HeroPowerHandHelper.IsHeroPowerCard 语义一致——
    /// <b>新增英雄技能卡自动排除</b>，无需维护排除列表。
    /// </summary>
    private static bool IsHeroPowerCardType(System.Type type)
    {
        var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(type));
        return canonical != null && jaina.Scripts.Character.Powers.HeroPowerHandHelper.IsHeroPowerCard(canonical);
    }

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
            yield return HoverTipFactory.FromKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Discover);
            if (IsUpgraded)
            {
                yield break;
            }
            yield return new CardHoverTip(ModelDb.Card<Frostbolt>());
            yield return new CardHoverTip(ModelDb.Card<ArcaneIntellect>());
            yield return new CardHoverTip(ModelDb.Card<Fireball>());
        }
    }

    /// <summary>
    /// 升级为倒带：加入奥术派系（升级形态无派系→奥术）。
    /// LocalKeywords 懒缓存可能已在未升级状态初始化——需显式 AddKeyword(Arcane)。
    /// </summary>
    protected override void OnUpgrade()
    {
        AddKeyword(jaina.Scripts.Character.Keywords.JainaKeywords.Arcane);
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
            // （描述明确写三张卡名，固定池见 JainaDiscoverHelper.JainasGiftFixedPool）
            await JainaDiscoverHelper.DiscoverJainasGift(choiceContext, base.Owner);
        }
    }

    /// <summary>
    /// 升级形态（倒带）：发现一张本局施放过的其他攻击牌或技能牌的一张复制。
    /// 与 Rewind 逻辑一致（排除自身与<b>全部</b>英雄技能卡）。
    /// </summary>
    private async Task PlayAsRewind(PlayerChoiceContext choiceContext)
    {
        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var rec = jaina.Scripts.Character.JainaCastTracker.For(combatState);
        // 候选池 = 我施放过的攻击/技能牌（按玩家区分，联机不混入队友的）
        var playedSet = rec.PlayedAttackSkillsByPlayer.TryGetValue(base.Owner.NetId, out var myPlayed)
            ? myPlayed
            : new HashSet<System.Type>();
        var playedTypes = playedSet
            .Where(t => t != typeof(JainasGiftCard) && !IsHeroPowerCardType(t))
            .ToList();
        if (playedTypes.Count == 0)
        {
            return;
        }

        var rng = base.Owner.RunState.Rng.CombatTargets;
        var pool = new List<System.Type>(playedTypes);
        var candidates = new List<CardModel>();
        var playedUpgrades = rec.PlayedUpgradeLevelsByPlayer.TryGetValue(base.Owner.NetId, out var myUpgrades)
            ? myUpgrades
            : new Dictionary<System.Type, int>();
        while (candidates.Count < 3 && pool.Count > 0)
        {
            var type = rng.NextItem(pool);
            if (type == null)
            {
                break;
            }
            pool.Remove(type);
            playedUpgrades.TryGetValue(type, out var upgradeLevel);
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
