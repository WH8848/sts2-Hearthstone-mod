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
    /// 单场战斗的施放记录。
    /// 联机注意：所有"按玩家语义"的记录都按玩家 NetId 区分——
    /// 两端都会对每个玩家的卡执行 RecordPlayed（确定性），
    /// 读取时用 owner.NetId 取自己的记录，避免误用队友的数据。
    /// </summary>
    public sealed class CombatRecord
    {
        /// <summary>
        /// 各玩家施放过的攻击/技能牌类型（吉安娜的礼物倒带候选池用）。
        /// </summary>
        public readonly Dictionary<ulong, HashSet<Type>> PlayedAttackSkillsByPlayer = [];

        /// <summary>
        /// 各玩家生成过的攻击/技能牌类型（"牌库之外"类型级判定用，罗曼斯/任务线）。
        /// </summary>
        public readonly Dictionary<ulong, HashSet<Type>> GeneratedAttackSkillsByPlayer = [];

        /// <summary>
        /// 各玩家手打的"牌库之外"攻击/技能牌次数（按类型计数，罗曼斯重放用）。
        /// 只计玩家手打（排除罗曼斯重放等自动打出）。
        /// </summary>
        public readonly Dictionary<ulong, Dictionary<Type, int>> PlayerCastOutsideDeckCountsByPlayer = [];

        /// <summary>
        /// 各玩家施放过的牌的最高升级级别（倒带复制时恢复升级状态）
        /// </summary>
        public readonly Dictionary<ulong, Dictionary<Type, int>> PlayedUpgradeLevelsByPlayer = [];

        /// <summary>
        /// 各玩家生成过的牌的最高升级级别（罗曼斯重放时恢复升级状态）
        /// </summary>
        public readonly Dictionary<ulong, Dictionary<Type, int>> GeneratedUpgradeLevelsByPlayer = [];

        public readonly HashSet<JainaSpellSchool> Schools = [];

        /// <summary>
        /// 各玩家最近施放的一个"费用消耗 ≥ 2"的法术牌（灰贤鹦鹉战吼重复用）。
        /// 记录 (类型, 施放时的升级级别, 是否本局衍生)。
        /// </summary>
        public readonly Dictionary<ulong, (Type Type, int UpgradeLevel, bool IsGenerated)?> LastCastSpellCost2PlusByPlayer = [];

        /// <summary>
        /// 各玩家施放过的"灯光表演"（升级版奥术弹幕）次数（灯光表演的光束数递增用）
        /// </summary>
        public readonly Dictionary<ulong, int> LightshowCastsByPlayer = [];

        /// <summary>
        /// 各玩家各法术派系最近施放过的法术（魔导师晨拥战吼重放用）。
        /// 记录 (类型, 施放时的升级级别, 是否本局衍生)。
        /// </summary>
        public readonly Dictionary<ulong, Dictionary<JainaSpellSchool, (Type Type, int UpgradeLevel, bool IsGenerated)>> LastCastBySchoolByPlayer = [];

        /// <summary>
        /// 各玩家奥术爆裂（英雄技能）本局已打出次数（每次打出 +2 伤害）
        /// </summary>
        public readonly Dictionary<ulong, int> ArcaneBurstCastsByPlayer = [];

        /// <summary>
        /// 各玩家当前英雄技能类型（打出英雄卡后替换；null = 默认火焰冲击）。
        /// </summary>
        public readonly Dictionary<ulong, System.Type?> CurrentHeroPowerTypeByPlayer = [];

        /// <summary>
        /// 各玩家本局对战中英雄技能累计造成的伤害（火眼莫德雷斯战吼条件用）。
        /// </summary>
        public readonly Dictionary<ulong, int> HeroPowerDamageDealtByPlayer = [];

        /// <summary>
        /// 各玩家本局对战中死亡过的不稳定的骷髅数量（天定之灾克尔苏加德战吼用）。
        /// </summary>
        public readonly Dictionary<ulong, int> SkeletonDeathsByPlayer = [];

        /// <summary>
        /// 各玩家最近施放的一张攻击/技能牌（蓄谋诈骗犯战吼"再次使用你使用过的上一张卡牌"用）。
        /// 记录 (类型, 施放时的升级级别, 是否本局衍生)；未施放过为 null。
        /// </summary>
        public readonly Dictionary<ulong, (Type Type, int UpgradeLevel, bool IsGenerated)?> LastPlayedCardByPlayer = [];

        /// <summary>
        /// 取某玩家的类型集合（不存在则创建）
        /// </summary>
        public HashSet<Type> SetFor(Dictionary<ulong, HashSet<Type>> map, ulong netId)
        {
            if (!map.TryGetValue(netId, out var set))
            {
                set = [];
                map[netId] = set;
            }
            return set;
        }

        /// <summary>
        /// 取某玩家的嵌套字典（不存在则创建）
        /// </summary>
        public Dictionary<TKey, TValue> MapFor<TKey, TValue>(Dictionary<ulong, Dictionary<TKey, TValue>> map, ulong netId)
            where TKey : notnull
        {
            if (!map.TryGetValue(netId, out var m))
            {
                m = [];
                map[netId] = m;
            }
            return m;
        }
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
        [typeof(RayOfFrostCard)] = JainaSpellSchool.Frost,
        [typeof(IceBlockCard)] = JainaSpellSchool.Frost,
        [typeof(AmazingDeckCard)] = JainaSpellSchool.Arcane,
        [typeof(IceBarrier)] = JainaSpellSchool.Frost,
        [typeof(DeepFreezeCard)] = JainaSpellSchool.Frost,
        [typeof(ArcaneIntellect)] = JainaSpellSchool.Arcane,
        [typeof(IncantersFlowCard)] = JainaSpellSchool.Arcane,
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
    /// 卡牌打出时记录（各攻击/技能牌 OnPlay 首行调用）。
    /// 法术牌 = 攻击牌/技能牌，或挂"法术牌"关键词的卡（任务线卡、寒冰屏障等能力卡视为法术牌，
    /// 可被倒带/西瓦拉复制）。
    /// </summary>
    public static void RecordPlayed(CardModel card)
    {
        var state = card.CombatState ?? card.Owner?.Creature.CombatState;
        if (state == null)
        {
            return;
        }
        if (card.Type != CardType.Attack && card.Type != CardType.Skill &&
            !card.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Spell))
        {
            return;
        }
        var rec = For(state);
        var type = card.GetType();
        var ownerId = card.Owner.NetId;
        rec.SetFor(rec.PlayedAttackSkillsByPlayer, ownerId).Add(type);
        // 记录"上一张施放的攻击/技能牌"（蓄谋诈骗犯战吼重放用）——按玩家区分，
        // 联机时每个玩家只重放自己施放的上一张牌
        rec.LastPlayedCardByPlayer[ownerId] = (type, card.CurrentUpgradeLevel, IsGeneratedCard(card));
        var playedUpgrades = rec.MapFor(rec.PlayedUpgradeLevelsByPlayer, ownerId);
        if (card.CurrentUpgradeLevel > 0 &&
            (!playedUpgrades.TryGetValue(type, out var prev) || card.CurrentUpgradeLevel > prev))
        {
            playedUpgrades[type] = card.CurrentUpgradeLevel;
        }
        // 罗曼斯重放计数：只计"玩家手打的牌库之外法术"。
        // 排除罗曼斯重放等自动打出（AutoPlayMarkPatch 已标记），避免重放自身导致计数膨胀。
        if (IsOutsideDeckCard(card) && !jaina.Scripts.Character.Powers.RommathReplayTracker.IsMarked(card))
        {
            var counts = rec.MapFor(rec.PlayerCastOutsideDeckCountsByPlayer, ownerId);
            counts.TryGetValue(type, out var n);
            counts[type] = n + 1;
        }
        if (SchoolByCardType.TryGetValue(type, out var school))
        {
            rec.Schools.Add(school);
            // 记录该派系最近施放的法术（魔导师晨拥战吼重放用）——按玩家区分
            rec.MapFor(rec.LastCastBySchoolByPlayer, ownerId)[school] = (type, card.CurrentUpgradeLevel, IsGeneratedCard(card));
        }
        // 灰贤鹦鹉：记录最近施放的"费用消耗 ≥ 2"的法术牌（按施放时的升级级别与本局衍生状态）
        // 用 Canonical（基础费用，含升级调整）判定——临时减费（巫师学徒等）不改变"≥2"语义的稳定性
        if (card.EnergyCost.Canonical >= 2)
        {
            rec.LastCastSpellCost2PlusByPlayer[ownerId] = (type, card.CurrentUpgradeLevel, IsGeneratedCard(card));
        }
    }

    /// <summary>
    /// 标记一张"牌库之外"生成的卡（AddGeneratedCardToCombat 前调用，罗曼斯重放用）。
    /// 实例级标记（法术/随从卡蓝光用）对所有衍生卡生效；
    /// 类型级记录（罗曼斯重放）仅对攻击/技能牌生效。
    /// 吉安娜局内衍生出来的卡全部附加"消耗"（Exhaust）关键词：
    /// 打出后进入消耗堆而不是弃牌堆（炉石语义：衍生物打出即消失）。
    /// </summary>
    public static void MarkGenerated(CardModel card)
    {
        // 实例级标记：本局对战内衍生出来的卡（蓝光判定用，含随从卡）
        GeneratedCardInstances.Remove(card);
        GeneratedCardInstances.Add(card, null!);

        // 吉安娜局内衍生卡自动附加消耗（打出后消耗，不回弃牌堆）。
        // 仅对可变的战斗实例生效（canonical 模板不可修改，且无需标记）。
        if (card.IsMutable)
        {
            try
            {
                MegaCrit.Sts2.Core.Commands.CardCmd.ApplyKeyword(card, CardKeyword.Exhaust);
            }
            catch
            {
                // 附加消耗失败不影响生成流程（罕见：不可变模板/已移除卡）
            }
        }

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
        var ownerId = card.Owner.NetId;
        rec.SetFor(rec.GeneratedAttackSkillsByPlayer, ownerId).Add(type);
        var genUpgrades = rec.MapFor(rec.GeneratedUpgradeLevelsByPlayer, ownerId);
        if (card.CurrentUpgradeLevel > 0 &&
            (!genUpgrades.TryGetValue(type, out var prev) || card.CurrentUpgradeLevel > prev))
        {
            genUpgrades[type] = card.CurrentUpgradeLevel;
        }
    }

    /// <summary>
    /// 记录英雄技能造成的伤害（火眼莫德雷斯战吼条件用）。
    /// 火焰冲击/二级火焰冲击/奥术爆裂/冰冷触摸造成伤害后调用。
    /// </summary>
    public static void RecordHeroPowerDamage(CardModel card, int damage)
    {
        if (damage <= 0)
        {
            return;
        }
        var state = card.CombatState ?? card.Owner?.Creature.CombatState;
        if (state == null || card.Owner == null)
        {
            return;
        }
        var rec = For(state);
        rec.HeroPowerDamageDealtByPlayer.TryGetValue(card.Owner.NetId, out var total);
        rec.HeroPowerDamageDealtByPlayer[card.Owner.NetId] = total + damage;
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
    /// 类型记录按卡所属玩家区分（联机：队友生成的类型不算我的"牌库之外"）。
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
        if (state == null || card.Owner == null)
        {
            return false;
        }
        var rec = For(state);
        return rec.GeneratedAttackSkillsByPlayer.TryGetValue(card.Owner.NetId, out var set) &&
               set.Contains(card.GetType());
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
    /// 创建当前英雄技能的副本（鲁莽的学徒/小精灵驾驭者战吼自动打出用）：
    /// 从手牌中同类型的英雄技能卡取升级等级（手牌没有则 0 级），创建副本。
    /// 副本打出后由调用方移出牌堆——手牌中的英雄技能卡不受影响，
    /// 不会额外生成英雄技能卡（打出的是与手牌同一张卡的副本）。
    /// </summary>
    public static MegaCrit.Sts2.Core.Models.CardModel? CreateHeroPowerCopy(
        ICombatState combatState, Player owner, Type heroPowerType)
    {
        int upgradeLevel = 0;
        var hand = owner.PlayerCombatState?.Hand;
        if (hand != null)
        {
            foreach (var c in hand.Cards)
            {
                if (c != null && c.GetType() == heroPowerType &&
                    Powers.HeroPowerHandHelper.IsHeroPowerCard(c))
                {
                    upgradeLevel = c.CurrentUpgradeLevel;
                    break;
                }
            }
        }
        return CreateCardWithUpgrade(combatState, owner, heroPowerType, upgradeLevel);
    }

    /// <summary>
    /// 火焰/奥术/冰霜三派系是否都已施放过
    /// </summary>
    public static bool HasAllThreeSchools(ICombatState combatState)
    {
        var rec = Records.TryGetValue(combatState, out var r) ? r : null;
        return rec != null && rec.Schools.Count >= 3;
    }

    /// <summary>
    /// 发现/随机生成池中该法术牌允许出现的最高升级级别：
    /// 未升级形态与升级形态（+）都可能被检索，但升级级别上限 2 级。
    /// 点燃（Ignite）可无限升级，但发现/随机生成只能获得未升级形态（0 级）——
    /// 只有倒带/西瓦拉这类"复制具体施放过的牌"的效果才保留其实际升级层数。
    /// </summary>
    public static int GetDiscoverPoolMaxUpgradeLevel(Type cardType)
    {
        if (cardType == typeof(IgniteCard))
        {
            return 0;
        }
        var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(cardType));
        if (canonical == null)
        {
            return 0;
        }
        return Math.Min(canonical.MaxUpgradeLevel, 2);
    }
}
