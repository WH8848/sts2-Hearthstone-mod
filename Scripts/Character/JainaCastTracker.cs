using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;

namespace jaina.Scripts.Character;

/// <summary>
/// 法术派系（对应文档"额外描述"列的火焰/奥术/冰霜标签）
/// </summary>
public enum JainaSpellSchool
{
    Fire,
    Frost,
    Arcane
}

/// <summary>
/// 本局对战施放追踪器（倒带/罗曼斯/诺甘农的智慧用）。
/// 每场战斗一份记录（ConditionalWeakTable 随战斗结束自动清理）：
/// - 施放过的攻击/技能牌类型（倒带"施放过的其他攻击牌或技能牌"）
/// - 施放过的"牌库之外"攻击/技能牌类型（罗曼斯重放）
/// - 施放过的法术派系（诺甘农的智慧降费）
/// </summary>
public static class JainaCastTracker
{
    /// <summary>
    /// 单场战斗的施放记录
    /// </summary>
    public sealed class CombatRecord
    {
        public readonly HashSet<Type> PlayedAttackSkills = [];
        public readonly HashSet<Type> GeneratedAttackSkills = [];

        /// <summary>
        /// 玩家手打的"牌库之外"攻击/技能牌次数（按类型计数，罗曼斯重放用）。
        /// 只计玩家手打（排除罗曼斯重放等自动打出），与打开时空之门"只计手打"语义一致；
        /// 罗曼斯按此计数重放每种类型多次（炉石：每次施放都重放）。
        /// </summary>
        public readonly Dictionary<Type, int> PlayerCastOutsideDeckCounts = [];

        /// <summary>
        /// 施放过的牌的最高升级级别（倒带复制时恢复升级状态，如清凉的泉水）
        /// </summary>
        public readonly Dictionary<Type, int> PlayedUpgradeLevels = [];

        /// <summary>
        /// 生成过的牌的最高升级级别（罗曼斯重放时恢复升级状态）
        /// </summary>
        public readonly Dictionary<Type, int> GeneratedUpgradeLevels = [];

        public readonly HashSet<JainaSpellSchool> Schools = [];

        /// <summary>
        /// 最近施放的一个"费用消耗 ≥ 2"的法术牌（灰贤鹦鹉战吼重复用）。
        /// 记录 (类型, 施放时的升级级别, 是否本局衍生)。
        /// </summary>
        public (Type Type, int UpgradeLevel, bool IsGenerated)? LastCastSpellCost2Plus;
    }

    private static readonly ConditionalWeakTable<ICombatState, CombatRecord> Records = new();

    /// <summary>
    /// 本局对战内衍生出来的卡牌实例（法术蓝光判定用；弱引用随卡回收自动清理）
    /// </summary>
    private static readonly ConditionalWeakTable<CardModel, object> GeneratedCardInstances = new();

    /// <summary>
    /// 时空扭曲"每局对战限一次"记录（弱引用随战斗结束自动清理）
    /// </summary>
    private static readonly ConditionalWeakTable<ICombatState, object> TimeWarpUsed = new();

    /// <summary>
    /// 本局对战是否已使用过时空扭曲（每局对战限一次）
    /// </summary>
    public static bool IsTimeWarpUsedThisCombat(ICombatState combatState)
    {
        return TimeWarpUsed.TryGetValue(combatState, out _);
    }

    /// <summary>
    /// 标记本局对战已使用过时空扭曲
    /// </summary>
    public static void MarkTimeWarpUsed(ICombatState combatState)
    {
        TimeWarpUsed.Remove(combatState);
        TimeWarpUsed.Add(combatState, null!);
    }

    /// <summary>
    /// 卡牌类型 → 法术派系（未列出的攻击/技能牌无派系）。
    /// 注意：火焰冲击是英雄技能，不属于法术牌，不计入派系。
    /// </summary>
    private static readonly Dictionary<Type, JainaSpellSchool> SchoolByCardType = new()
    {
        [typeof(Fireball)] = JainaSpellSchool.Fire,
        [typeof(IgniteCard)] = JainaSpellSchool.Fire,
        [typeof(Frostbolt)] = JainaSpellSchool.Frost,
        [typeof(FreezingPotion)] = JainaSpellSchool.Frost,
        [typeof(IceBarrier)] = JainaSpellSchool.Frost,
        [typeof(DeepFreezeCard)] = JainaSpellSchool.Frost,
        [typeof(ArcaneIntellect)] = JainaSpellSchool.Arcane,
        [typeof(Rewind)] = JainaSpellSchool.Arcane,
        [typeof(JainasGiftCard)] = JainaSpellSchool.Arcane,
        [typeof(Trick)] = JainaSpellSchool.Arcane,
        [typeof(Awaken)] = JainaSpellSchool.Arcane,
        [typeof(NorgannonWisdom)] = JainaSpellSchool.Arcane,
        [typeof(Objection)] = JainaSpellSchool.Arcane
    };

    /// <summary>
    /// 取本场战斗的追踪记录
    /// </summary>
    public static CombatRecord For(ICombatState combatState) => Records.GetValue(combatState, _ => new CombatRecord());

    /// <summary>
    /// 卡牌打出时记录（各攻击/技能牌 OnPlay 首行调用）
    /// </summary>
    public static void RecordPlayed(CardModel card)
    {
        var state = card.CombatState ?? card.Owner?.Creature.CombatState;
        if (state == null)
        {
            return;
        }
        if (card.Type != CardType.Attack && card.Type != CardType.Skill)
        {
            return;
        }
        var rec = For(state);
        var type = card.GetType();
        rec.PlayedAttackSkills.Add(type);
        if (card.CurrentUpgradeLevel > 0 &&
            (!rec.PlayedUpgradeLevels.TryGetValue(type, out var prev) || card.CurrentUpgradeLevel > prev))
        {
            rec.PlayedUpgradeLevels[type] = card.CurrentUpgradeLevel;
        }
        // 罗曼斯重放计数：只计"玩家手打的牌库之外法术"。
        // 排除罗曼斯重放等自动打出（AutoPlayMarkPatch 已标记），避免重放自身导致计数膨胀。
        if (IsOutsideDeckCard(card) && !jaina.Scripts.Character.Powers.RommathReplayTracker.IsMarked(card))
        {
            rec.PlayerCastOutsideDeckCounts.TryGetValue(type, out var n);
            rec.PlayerCastOutsideDeckCounts[type] = n + 1;
        }
        if (SchoolByCardType.TryGetValue(type, out var school))
        {
            rec.Schools.Add(school);
        }
        // 灰贤鹦鹉：记录最近施放的"费用消耗 ≥ 2"的法术牌（按施放时的升级级别与本局衍生状态）
        // 用 Canonical（基础费用，含升级调整）判定——临时减费（巫师学徒等）不改变"≥2"语义的稳定性
        if (card.EnergyCost.Canonical >= 2)
        {
            rec.LastCastSpellCost2Plus = (type, card.CurrentUpgradeLevel, IsGeneratedCard(card));
        }
    }

    /// <summary>
    /// 标记一张"牌库之外"生成的卡（AddGeneratedCardToCombat 前调用，罗曼斯重放用）。
    /// 实例级标记（法术/随从卡蓝光用）对所有衍生卡生效；
    /// 类型级记录（罗曼斯重放）仅对攻击/技能牌生效。
    /// </summary>
    public static void MarkGenerated(CardModel card)
    {
        // 实例级标记：本局对战内衍生出来的卡（蓝光判定用，含随从卡）
        GeneratedCardInstances.Remove(card);
        GeneratedCardInstances.Add(card, null!);

        if (card.Type != CardType.Attack && card.Type != CardType.Skill)
        {
            return;
        }
        var state = card.CombatState ?? card.Owner?.Creature.CombatState;
        if (state == null)
        {
            return;
        }
        var rec = For(state);
        var type = card.GetType();
        rec.GeneratedAttackSkills.Add(type);
        if (card.CurrentUpgradeLevel > 0 &&
            (!rec.GeneratedUpgradeLevels.TryGetValue(type, out var prev) || card.CurrentUpgradeLevel > prev))
        {
            rec.GeneratedUpgradeLevels[type] = card.CurrentUpgradeLevel;
        }
    }

    /// <summary>
    /// 该卡实例是否为本局对战内衍生出来的（牌库之外的卡）
    /// </summary>
    public static bool IsGeneratedCard(CardModel card)
    {
        return GeneratedCardInstances.TryGetValue(card, out _);
    }

    /// <summary>
    /// 该攻击/技能牌是否"牌库之外"（本局对战内生成过的类型，含实例标记或类型记录）。
    /// 实例标记覆盖生成时记录过的卡；类型记录覆盖罗曼斯重放等漏标实例的卡。
    /// </summary>
    public static bool IsOutsideDeckCard(CardModel card)
    {
        if (card.Type != CardType.Attack && card.Type != CardType.Skill)
        {
            return false;
        }
        if (GeneratedCardInstances.TryGetValue(card, out _))
        {
            return true;
        }
        var state = card.CombatState ?? card.Owner?.Creature.CombatState;
        if (state == null)
        {
            return false;
        }
        return For(state).GeneratedAttackSkills.Contains(card.GetType());
    }

    /// <summary>
    /// 按记录的最高升级级别创建一张牌的实例（倒带/罗曼斯复制用）：
    /// 用 canonical 模板创建后逐级升级，恢复"清凉的泉水"这类升级形态。
    /// 找不到模板返回 null。
    /// </summary>
    public static MegaCrit.Sts2.Core.Models.CardModel? CreateCardWithUpgrade(
        ICombatState combatState, Player owner, Type type, int upgradeLevel)
    {
        var canonical = MegaCrit.Sts2.Core.Models.ModelDb.GetByIdOrNull<MegaCrit.Sts2.Core.Models.CardModel>(
            MegaCrit.Sts2.Core.Models.ModelDb.GetId(type));
        if (canonical == null)
        {
            return null;
        }
        var card = combatState.CreateCard(canonical, owner);
        for (int i = 0; i < upgradeLevel && card.CurrentUpgradeLevel < card.MaxUpgradeLevel; i++)
        {
            MegaCrit.Sts2.Core.Commands.CardCmd.Upgrade(card);
        }
        return card;
    }

    /// <summary>
    /// 火焰/奥术/冰霜三派系是否都已施放过
    /// </summary>
    public static bool HasAllThreeSchools(ICombatState combatState)
    {
        var rec = Records.TryGetValue(combatState, out var r) ? r : null;
        return rec != null && rec.Schools.Count >= 3;
    }
}
