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
/// 本局对战施放追踪器（倒带/罗曼斯/诺干农的智慧用）。
/// 每场战斗一份记录（ConditionalWeakTable 随战斗结束自动清理）：
/// - 施放过的攻击/技能牌类型（倒带"施放过的其他攻击牌或技能牌"）
/// - 施放过的"牌库之外"攻击/技能牌类型（罗曼斯重放）
/// - 施放过的法术派系（诺干农的智慧降费）
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
        /// 施放过的牌的最高升级级别（倒带复制时恢复升级状态，如清凉的泉水）
        /// </summary>
        public readonly Dictionary<Type, int> PlayedUpgradeLevels = [];

        /// <summary>
        /// 生成过的牌的最高升级级别（罗曼斯重放时恢复升级状态）
        /// </summary>
        public readonly Dictionary<Type, int> GeneratedUpgradeLevels = [];

        public readonly HashSet<JainaSpellSchool> Schools = [];
    }

    private static readonly ConditionalWeakTable<ICombatState, CombatRecord> Records = new();

    /// <summary>
    /// 卡牌类型 → 法术派系（未列出的攻击/技能牌无派系）。
    /// 注意：火焰冲击是英雄技能，不属于法术牌，不计入派系。
    /// </summary>
    private static readonly Dictionary<Type, JainaSpellSchool> SchoolByCardType = new()
    {
        [typeof(Fireball)] = JainaSpellSchool.Fire,
        [typeof(Frostbolt)] = JainaSpellSchool.Frost,
        [typeof(FreezingPotion)] = JainaSpellSchool.Frost,
        [typeof(IceBarrier)] = JainaSpellSchool.Frost,
        [typeof(ArcaneIntellect)] = JainaSpellSchool.Arcane,
        [typeof(Rewind)] = JainaSpellSchool.Arcane,
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
        if (SchoolByCardType.TryGetValue(type, out var school))
        {
            rec.Schools.Add(school);
        }
    }

    /// <summary>
    /// 标记一张"牌库之外"生成的攻击/技能牌（AddGeneratedCardToCombat 前调用，罗曼斯重放用）
    /// </summary>
    public static void MarkGenerated(CardModel card)
    {
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
