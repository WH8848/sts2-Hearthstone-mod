using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Powers;
using MinionLib.Targeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 禁忌神龛 (Forbidden Shrine) - X费技能牌（罕见）。
/// 随机施放一个 x 费卡牌（x = 消耗的能量，无上限），<b>精确费用匹配</b>。
/// 例外：<b>X 费卡牌</b>（HasEnergyCostX，如挽歌 Dirge——消耗全部能量的卡）
/// 无论玩家用多少费打出禁忌神龛都有机会被打出（它们"匹配任意 X"）。
/// 升级后（禁忌神龛+）：随机施放一个 x+1 费卡牌（无上限）。
/// 卡牌 = <b>全角色</b>可打出卡牌（法术/能力牌，Attack/Skill/Power 类型，含升级形态；
/// 不保留随从/地标/武器/吉安娜非法术能力牌），排除英雄技能卡、任务卡、先古/衍生池
/// （IsEligible）、多人专属卡、自身，以及<b>X 星卡</b>（储君的 0 能量 X 星卡如星尘——
/// 消耗星星而非能量，只有 0 费打出禁忌神龛（X=0）时才有机会被打出）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class ForbiddenShrineCard : JainaSpellCardTemplate
{
    /// <summary>
    /// X 费用：自动消耗玩家全部剩余能量
    /// </summary>
    protected override bool HasEnergyCostX => true;

    /// <summary>
    /// 法术牌
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath => "res://assets/card_art/forbidden_shrine.png";

    public ForbiddenShrineCard()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"禁忌神龛+"
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
            return title.GetFormattedText() + "+";
        }
    }

    /// <summary>
    /// 随机施放一个目标费用的全角色卡牌：动态构建（ModelDb.AllCards 的
    /// Attack/Skill/Power 类型——法术/能力牌，不含随从/地标/武器（"武器"关键词排除），
    /// 含升级形态，IsEligible 统一排除 + 英雄技能卡、多人专属卡与自身）。
    /// 费用匹配：普通卡需当前基础费用 == targetCost；<b>X 费卡</b>
    /// （HasEnergyCostX，如挽歌——消耗全部能量的卡）始终入选（匹配任意 X）。
    /// X 星卡（HasStarCostX——储君的 0 能量 X 星卡如星尘）只在 0 费打出（X=0）时入选。
    /// </summary>
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }

        // x = 消耗的能量；升级后目标费用 x+1（无上限——花多少能量就施放多少费的卡牌）
        int x = ResolveEnergyXValue();
        int targetCost = IsUpgraded ? x + 1 : x;
        // 0 费打出禁忌神龛（X=0）：X 星卡（星尘等 0 能量 X 星卡）才有机会被打出
        bool allowStarCostX = targetCost == 0;

        // 收集目标费用的<b>全角色</b>卡牌（Attack/Skill/Power，含升级形态），
        // 应用 Jaina 随机池统一排除（8 个非角色/衍生池、任务卡、先古稀有度、多人专属，见
        // JainaRandomPoolHelper.IsEligible），排除英雄技能卡、自身（同名不可自发现）。
        // 费用匹配用当前基础费用（含升级减费）：升级后减费到目标费用的形态也入选；
        // X 费卡（HasEnergyCostX）无论 X 是多少都入选（消耗全部能量，匹配任意 X）。
        var pool = new List<CardModel>();
        foreach (var canonical in MegaCrit.Sts2.Core.Models.ModelDb.AllCards)
        {
            if (canonical == null)
            {
                continue;
            }
            if (canonical.Type != CardType.Attack && canonical.Type != CardType.Skill &&
                canonical.Type != CardType.Power)
            {
                continue;
            }
            if (jaina.Scripts.Character.Powers.HeroPowerHandHelper.IsHeroPowerCard(canonical))
            {
                continue;
            }
            // 吉安娜非法术能力牌（戏法图腾/炉石形态——吉安娜池 Power 无"法术牌"关键词）
            // 不施放；其他角色/原版能力牌不在吉安娜池，保留（IsExcludedFromSpellPool 动态判定）
            if (jaina.Scripts.Character.JainaCastTracker.IsExcludedFromSpellPool(canonical.GetType()))
            {
                continue;
            }
            // 武器卡（Power 类型 + "武器"关键词）不施放：随机施放仅限法术/能力牌
            if (canonical.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Weapon) == true)
            {
                continue;
            }
            if (!jaina.Scripts.Character.JainaRandomPoolHelper.IsEligible(canonical))
            {
                continue;
            }
            if (canonical.GetType() == typeof(ForbiddenShrineCard))
            {
                continue;
            }
            // X 星卡（0 能量 X 星卡——储君的星尘等）：消耗星星而非能量，
            // 默认不施放；只有 0 费打出禁忌神龛（X=0）时才有机会被打出
            if (canonical.HasStarCostX && !allowStarCostX)
            {
                continue;
            }
            // X 费卡（EnergyCost.CostsX，如挽歌——消耗全部能量）：始终入选（匹配任意 X）
            bool isEnergyX = canonical.EnergyCost.CostsX;
            int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(canonical.GetType());
            for (int level = 0; level <= maxLevel; level++)
            {
                var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                    combatState, base.Owner, canonical.GetType(), level);
                if (card == null)
                {
                    continue;
                }
                if (!isEnergyX &&
                    card.EnergyCost.GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers.None) != targetCost)
                {
                    continue;
                }
                pool.Add(card);
            }
        }
        if (pool.Count == 0)
        {
            return;
        }
        var chosen = base.Owner.RunState.Rng.CombatCardSelection.NextItem(pool);
        if (chosen == null)
        {
            return;
        }
        jaina.Scripts.Character.JainaCastTracker.MarkGenerated(chosen);

        // 单目标法术：随机选合法目标（尽可能以敌人为目标——有合法敌人只打敌人；
        // 无敌人时回退全部合法目标，不放弃施放，与魔法智慧之球统一语义）
        Creature? target = null;
        if (chosen.TargetType == TargetType.AnyEnemy || chosen.TargetType == TargetType.AnyPlayer ||
            chosen.TargetType == TargetType.AnyAlly ||
            (CustomTargetTypeManager.TryGetCustomTargetType(chosen.TargetType, out var customType) &&
             customType.IsSingleTarget))
        {
            IEnumerable<Creature> targetPool = combatState.Creatures
                .Where(c => c != null && c.IsAlive && chosen.IsValidTarget(c));
            var enemies = targetPool.Where(c => c.Side != base.Owner.Creature.Side).ToList();
            if (enemies.Count > 0)
            {
                targetPool = enemies;
            }
            var targets = targetPool.ToList();
            target = targets.Count > 0 ? base.Owner.RunState.Rng.CombatTargets.NextItem(targets) : null;
            if (target == null)
            {
                return;
            }
        }
        await CardCmd.AutoPlay(choiceContext, chosen, target);
    }
}
