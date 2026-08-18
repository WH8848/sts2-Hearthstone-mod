using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
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
/// 匣中古神 (Yogg in the Box) - 3费技能牌（罕见，暗影派系）。
/// 随机施放 5 个法术。如果你的抽牌堆里没有随从牌，则这些法术的费用消耗大于或等于 2 点。
/// （目标随机，联机可打队友）
/// 升级后变为尤格-萨隆的谜之匣 (Puzzle Box of Yogg-Saron)：随机施放 10 个法术（目标随机，联机可打队友）。
/// </summary>
[RegisterCard(typeof(JainaCardPool))]
public sealed class YoggBoxCard : JainaSpellCardTemplate
{
    /// <summary>
    /// 法术牌 + 暗影派系
    /// </summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [jaina.Scripts.Character.Keywords.JainaKeywords.Spell, jaina.Scripts.Character.Keywords.JainaKeywords.Shadow];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    public override string CustomPortraitPath =>
        IsUpgraded ? "res://assets/card_art/puzzle_box_yogg.png" : "res://assets/card_art/yogg_in_the_box.png";

    public YoggBoxCard()
        : base(3, CardType.Skill, CardRarity.Uncommon, TargetType.None, true)
    {
    }

    /// <summary>
    /// 升级后卡牌名称变为"尤格-萨隆的谜之匣"
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

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 记录施放（倒带/罗曼斯/三派系追踪）
        jaina.Scripts.Character.JainaCastTracker.RecordPlayed(this);

        int count = IsUpgraded ? 10 : 5;
        // 未升级：抽牌堆无随从牌 → 只选费用 ≥ 2 的法术
        bool cost2PlusOnly = !IsUpgraded && !HasMinionInDrawPile();
        await CastRandomSpells(choiceContext, count, cost2PlusOnly);
    }

    /// <summary>
    /// 抽牌堆中是否有随从牌
    /// </summary>
    private bool HasMinionInDrawPile()
    {
        return base.Owner.PlayerCombatState?.DrawPile.Cards.Any(
            c => c.Type == JainaCardTypes.Minion) ?? false;
    }

    /// <summary>
    /// 随机施放指定数量的法术：从吉安娜法术池随机取（不重复），随机目标（联机可打队友）。
    /// 每个法术免费自动打出；单目标法术从场上所有活物（含队友）中随机选合法目标。
    /// </summary>
    private async Task CastRandomSpells(PlayerChoiceContext choiceContext, int count, bool cost2PlusOnly)
    {
        var combatState = base.Owner.Creature.CombatState;
        if (combatState == null)
        {
            return;
        }
        var pool = BuildSpellPool(combatState, cost2PlusOnly);
        if (pool.Count == 0)
        {
            return;
        }

        // 随机取 count 个不重复的法术类型（池不足时有多少放多少）
        var rng = base.Owner.RunState.Rng.CombatTargets;
        var picked = new List<CardModel>();
        var remaining = new List<CardModel>(pool);
        while (picked.Count < count && remaining.Count > 0)
        {
            var card = rng.NextItem(remaining);
            if (card == null)
            {
                break;
            }
            remaining.Remove(card);
            picked.Add(card);
        }

        foreach (var card in picked)
        {
            // 单目标法术：目标可从全部活物（自己/队友角色、双方随从、敌人）中随机选择——
            // 合法目标优先（IsValidTarget）；其他角色的自定义目标类型无法判定时回退全部活物，
            // 保证卡牌总能施放（联机可打队友）。
            Creature? target = null;
            if (card.TargetType != TargetType.None)
            {
                var allCreatures = combatState.Creatures
                    .Concat(combatState.Players.SelectMany(p => p.PlayerCombatState?.Pets ?? []))
                    .Where(c => c != null && c.IsAlive)
                    .ToList();
                var legal = allCreatures.Where(c => card.IsValidTarget(c)).ToList();
                var targetPool = legal.Count > 0 ? legal : allCreatures;
                target = targetPool.Count > 0 ? rng.NextItem(targetPool) : null;
                if (target == null)
                {
                    continue;
                }
            }
            MegaCrit.Sts2.Core.Logging.Log.Info($"[Jaina] Yogg cast: {card.Id} type={card.TargetType} " +
                                                $"target={(target != null ? target.Name : "none")}");
            jaina.Scripts.Character.JainaCastTracker.MarkGenerated(card);
            jaina.Scripts.Character.Powers.RommathReplayTracker.Mark(card);
            await CardCmd.AutoPlay(choiceContext, card, target);
        }
    }

    /// <summary>
    /// 法术池：全角色攻击/技能牌（按费用过滤，含升级形态）。
    /// 排除英雄技能卡（火焰冲击等）——英雄技能不是可施放的法术牌。
    /// 排除非角色卡池（无色/诅咒/先古/状态/任务/事件/衍生池）、先古稀有度卡
    /// 与多人游戏专属卡。
    /// 每种按可升级级别展开：未升级形态与升级形态（+）都可能被施放。
    /// 返回带 Owner 的可打出实例。
    /// </summary>
    private List<CardModel> BuildSpellPool(ICombatState combatState, bool cost2PlusOnly)
    {
        var result = new List<CardModel>();
        foreach (var canonical in GetSpellPoolCanonicals())
        {
            int maxLevel = jaina.Scripts.Character.JainaCastTracker.GetDiscoverPoolMaxUpgradeLevel(canonical.GetType());
            for (int level = 0; level <= maxLevel; level++)
            {
                var card = jaina.Scripts.Character.JainaCastTracker.CreateCardWithUpgrade(
                    combatState, base.Owner, canonical.GetType(), level);
                if (card == null)
                {
                    continue;
                }
                if (cost2PlusOnly && card.EnergyCost.Canonical < 2)
                {
                    continue;
                }
                result.Add(card);
            }
        }
        return result;
    }

    /// <summary>
    /// 法术池的 canonical 卡列表（不含升级形态展开）：
    /// 全角色攻击/技能牌，排除英雄技能卡与所有 Jaina 随机池排除项
    /// （7 个非角色池/先古稀有度/多人专属，见 <see cref="JainaRandomPoolHelper"/>）。
    /// 供 <see cref="BuildSpellPool"/> 与诊断日志（Entry.RegisterYoggPoolDiag）复用。
    /// </summary>
    internal static List<CardModel> GetSpellPoolCanonicals()
    {
        var result = new List<CardModel>();
        foreach (var canonical in ModelDb.AllCards)
        {
            if (canonical == null)
            {
                continue;
            }
            if (canonical.Type != CardType.Attack && canonical.Type != CardType.Skill)
            {
                continue;
            }
            if (HeroPowerHandHelper.IsHeroPowerCard(canonical))
            {
                continue;
            }
            if (!jaina.Scripts.Character.JainaRandomPoolHelper.IsEligible(canonical))
            {
                continue;
            }
            result.Add(canonical);
        }
        return result;
    }
}
