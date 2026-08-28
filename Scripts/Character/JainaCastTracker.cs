using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using jaina.Scripts.Character.Cards;

namespace jaina.Scripts.Character;

/// <summary>
/// 法术派系（炉石标准 7 大派系：火焰/冰霜/奥术/暗影/邪能/神圣/自然）
/// </summary>
public enum JainaSpellSchool
{
    Fire,
    Frost,
    Arcane,
    Shadow,
    Fel,
    Holy,
    Nature
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
        /// 各玩家本局已施放的"灯光表演"次数（每次释放攻击次数 +1，按玩家区分）
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
        /// 各玩家被替换时继承的英雄技能升级伤害加成（累计，按玩家区分）。
        /// 玩家升级过英雄技能（火焰冲击/二级火焰冲击，伤害 +1/+2）后被打出英雄卡
        /// 替换（魔导师晨拥→奥术爆裂、冰霜女巫吉安娜→冰冷触摸）时，升级伤害增量随之
        /// 转移；再次替换沿袭累计值（只加"旧技能自身升级差量"，链式不丢失）。
        /// 替换流程两端同步执行同一动作，记录两端确定性一致。
        /// </summary>
        public readonly Dictionary<ulong, int> HeroPowerInheritedDamageByPlayer = [];

        /// <summary>
        /// 各玩家本局对战中英雄技能累计造成的伤害（火眼莫德雷斯战吼条件用）。
        /// </summary>
        public readonly Dictionary<ulong, int> HeroPowerDamageDealtByPlayer = [];

        /// <summary>
        /// 各玩家本局对战中死亡过的不稳定的骷髅数量（统计/预留）。
        /// </summary>
        public readonly Dictionary<ulong, int> SkeletonDeathsByPlayer = [];

    /// <summary>
    /// 英雄技能被替换时继承旧技能的<b>升级伤害增量</b>（按玩家累计）：
    /// 差量 = 旧技能卡伤害变量 BaseValue − 其形态基础值——火焰冲击升 1 次 +1（差 1）、
    /// 二级火焰冲击升 1 次 +2（差 2）；奥术爆裂/冰冷触摸/小精灵的祝福不可升级（差 0，
    /// 不重置累计——链式替换沿袭）。调用点：英雄卡替换（JainaHeroCardTemplate.OnPlay）、
    /// 灌注替换（EmpowerPower.AfterApplied）——两端同步执行同一动作，记录确定性一致。
    /// </summary>
    public void AccumulateInheritedHeroPowerDamage(
        ulong ownerNetId, IReadOnlyList<CardModel> oldHeroPowers, string logTag)
    {
        int inheritedDelta = 0;
        foreach (var old in oldHeroPowers)
        {
            if (old == null || old.DynamicVars.Damage == null)
            {
                continue;
            }
            var canonicalOld = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(old.GetType()));
            var canonicalBase = canonicalOld?.DynamicVars.Damage?.BaseValue ?? 0m;
            var oldDelta = old.DynamicVars.Damage.BaseValue - canonicalBase;
            if (oldDelta > 0)
            {
                inheritedDelta += (int)oldDelta;
            }
        }
        if (inheritedDelta > 0)
        {
            HeroPowerInheritedDamageByPlayer.TryGetValue(ownerNetId, out var prevInherited);
            HeroPowerInheritedDamageByPlayer[ownerNetId] = prevInherited + inheritedDelta;
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[{logTag}] inherited hero power upgrade damage +{inheritedDelta} (total={prevInherited + inheritedDelta}) for {ownerNetId}");
        }
    }

    /// <summary>
    /// 各玩家最近施放的一张攻击/技能牌（蓄谋诈骗犯战吼"再次使用你使用过的上一张卡牌"用）。
        /// 记录 (类型, 施放时的升级级别, 是否本局衍生)；未施放过为 null。
        /// </summary>
        public readonly Dictionary<ulong, (Type Type, int UpgradeLevel, bool IsGenerated)?> LastPlayedCardByPlayer = [];

        /// <summary>
        /// 各玩家<b>本回合</b>已手打的攻击/技能牌数量（卡雷苟斯/落难的大法师
        /// "当前回合第一张法术减费"判定用——随从登场时若本回合已打过法术，
        /// 减费窗口已过，不再给后续法术减费）。玩家回合开始清零。
        /// </summary>
        public readonly Dictionary<ulong, int> AttackOrSkillCountThisTurnByPlayer = [];

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
    /// 动态判定一张卡的派系（火焰/冰霜/奥术/暗影/邪能/神圣/自然）：按该卡实例的<b>本地</b>关键词
    /// （GetKeywordsWithSources(Local)，排除全局光环干扰）判断各派系。
    /// 每张卡只可能有一个派系（关键词互斥），按 Fire → Frost → Arcane → Shadow → Fel → Holy → Nature 顺序取第一个。
    /// 无派系返回 null。升级形态按实例关键词动态判定（如埃匹希斯冲击基础无派系、
    /// 升级为火焰——硬编码列表无法表达）。
    /// </summary>
    public static JainaSpellSchool? GetSchoolOf(CardModel card)
    {
        if (card == null)
        {
            return null;
        }
        var keywords = card.GetKeywordsWithSources(MegaCrit.Sts2.Core.Entities.Cards.KeywordSources.Local);
        if (keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Fire))
        {
            return JainaSpellSchool.Fire;
        }
        if (keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Frost))
        {
            return JainaSpellSchool.Frost;
        }
        if (keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Arcane))
        {
            return JainaSpellSchool.Arcane;
        }
        if (keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Shadow))
        {
            return JainaSpellSchool.Shadow;
        }
        if (keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Fel))
        {
            return JainaSpellSchool.Fel;
        }
        if (keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Holy))
        {
            return JainaSpellSchool.Holy;
        }
        if (keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Nature))
        {
            return JainaSpellSchool.Nature;
        }
        return null;
    }

    /// <summary>
    /// 取本场战斗的追踪记录
    /// </summary>
    public static CombatRecord For(ICombatState combatState) => Records.GetValue(combatState, _ => new CombatRecord());

    /// <summary>
    /// 该玩家<b>本回合</b>是否已手打过攻击/技能牌（卡雷苟斯/落难的大法师
    /// "当前回合第一张法术减费"窗口判定用——随从登场时已打过 → 窗口已过）。
    /// </summary>
    public static bool HasPlayedAttackOrSkillThisTurn(Player player)
    {
        if (player?.Creature?.CombatState == null || !Records.TryGetValue(player.Creature.CombatState, out var rec))
        {
            return false;
        }
        return rec.AttackOrSkillCountThisTurnByPlayer.TryGetValue(player.NetId, out var n) && n > 0;
    }

    /// <summary>
    /// 玩家回合开始：清零该玩家的"本回合攻击/技能牌计数"。
    /// 由卡雷苟斯/落难的大法师的 BeforeSideTurnStart 调用（幂等）。
    /// </summary>
    public static void ResetTurnAttackSkillCount(Player player)
    {
        if (player?.Creature?.CombatState == null || !Records.TryGetValue(player.Creature.CombatState, out var rec))
        {
            return;
        }
        rec.AttackOrSkillCountThisTurnByPlayer.Remove(player.NetId);
    }

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
        // 英雄技能卡（火焰冲击/二级火焰冲击/奥术爆裂/冰冷触摸）不记录：
        // 不被记作诈骗犯的"上一张"、不会被倒带等发现、不视为施放的法术（炉石规则）
        if (Powers.HeroPowerHandHelper.IsHeroPowerCard(card))
        {
            return;
        }
        var rec = For(state);
        var type = card.GetType();
        var ownerId = card.Owner.NetId;
        // 自动打出（AutoPlay）的卡不算"玩家手打"：匣中古神/惊奇卡牌/戏法图腾随机施放、
        // 罗曼斯/灰贤鹦鹉/诈骗犯重放等都不更新"上一张"（诈骗犯只重放玩家自己手打的卡）。
        bool isHandPlayed = !Powers.RommathReplayTracker.IsMarked(card);

        // "上一张"（蓄谋诈骗犯战吼重放用）：只记录玩家自己手打的<b>所有卡牌</b>
        // （法术/随从/英雄/地标/武器，按玩家区分——联机每个玩家只重放自己施放的上一张；
        // 英雄技能卡除外，上面已排除；随机/自动打出的卡不计入）
        if (isHandPlayed)
        {
            rec.LastPlayedCardByPlayer[ownerId] = (type, card.CurrentUpgradeLevel, IsGeneratedCard(card));
        }

        // 法术牌（统一判定：攻击/技能，或带"法术牌"关键词的能力牌）才进入攻击/技能池
        // 与派系/重放计数（倒带/罗曼斯/西瓦拉/派系追踪用）——随从/英雄/地标/武器卡只记录"上一张"
        bool isSpellCard = IsSpellCard(card);
        if (!isSpellCard)
        {
            return;
        }

        // 卡雷苟斯/落难的大法师"当前回合第一张法术"窗口判定：
        // 只计玩家手打的<b>攻击/技能牌</b>（随从卡/能力牌不计；自动打出不计——
        // 与 KalecgosPower.AfterCardPlayed 的 IsAutoPlay 排除一致）；玩家回合开始清零。
        if (isHandPlayed && (card.Type == CardType.Attack || card.Type == CardType.Skill))
        {
            rec.AttackOrSkillCountThisTurnByPlayer.TryGetValue(ownerId, out var n);
            rec.AttackOrSkillCountThisTurnByPlayer[ownerId] = n + 1;
        }

        // "本局施放过的攻击/技能牌"集合（吉安娜的礼物+倒带候选池用）：
        // 只计玩家手打（isHandPlayed）——随机/自动打出的卡（匣中古神/惊奇卡牌/
        // 戏法图腾/大法师的符文/罗曼斯/灰贤鹦鹉/诈骗犯重放等）不应成为倒带复制对象，
        // 否则符文随机打出的卡会被倒带错误发现。
        if (isHandPlayed)
        {
            rec.SetFor(rec.PlayedAttackSkillsByPlayer, ownerId).Add(type);
        }
        // 记录"上一张施放的攻击/技能牌"（蓄谋诈骗犯战吼重放用）——按玩家区分，
        // 联机时每个玩家只重放自己施放的上一张牌；只计手打（上面已记录，这里不再重复）
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
        // 派系：动态按卡实例关键词判定（Fire/Frost/Arcane，升级形态跟随实例）
        // 只计玩家手打——随机/自动打出的卡（符文等）不应成为晨拥重放对象
        // （否则符文随机打出的法术会被魔导师晨拥错误重放）
        if (isHandPlayed && GetSchoolOf(card) is { } school)
        {
            rec.Schools.Add(school);
            // 记录该派系最近施放的法术（魔导师晨拥战吼重放用）——按玩家区分
            rec.MapFor(rec.LastCastBySchoolByPlayer, ownerId)[school] = (type, card.CurrentUpgradeLevel, IsGeneratedCard(card));
        }
        // 灰贤鹦鹉：记录最近施放的"费用消耗 ≥ 2"的法术牌（按施放时的升级级别与本局衍生状态）
        // 只计玩家手打（isHandPlayed）——排除自动打出：罗曼斯/灰贤鹦鹉/诈骗犯重放、
        // 匣中古神/惊奇卡牌/戏法图腾/大法师的符文/冰血哨塔等随机施放的卡不应成为
        // 鹦鹉重复的对象（否则符文随机打出的卡会被鹦鹉错误重放）。
        // 用当前基础费用（含升级减费）判定——升级后减费到 <2 的形态不算"费用≥2"；
        // 临时减费（巫师学徒等）不改变判定（临时修正不影响 None）
        if (isHandPlayed &&
            card.EnergyCost.GetWithModifiers(MegaCrit.Sts2.Core.Entities.Cards.CostModifiers.None) >= 2)
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
    /// 统计一次攻击命令实际造成的总伤害（含力量/专注/易伤等修正）——
    /// 英雄技能伤害记录用（火眼莫德雷斯条件：记录真实命中伤害而非基础值）。
    /// 必须在 <c>Execute</c> 完成后调用（<see cref="AttackCommand.Results"/> 执行后才填充）。
    /// </summary>
    public static int SumActualDamage(AttackCommand attack)
    {
        return attack.Results.SelectMany(r => r).Sum(r => (int)r.TotalDamage);
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
    /// 附魔继承：旧英雄技能卡（被替换掉的）带附魔时，把附魔迁移到新英雄技能卡上
    /// （灌注→小精灵的祝福、英雄卡→奥术爆裂/冰冷触摸等替换流程）。
    /// 与原版 ArchaicTooth（火焰冲击→二级火焰冲击）的附魔迁移一致：
    /// 先 MutableClone 附魔模型（避免新旧两张卡共用同一附魔实例），再 CardCmd.Enchant。
    /// 新卡无法被该附魔附魔（附魔限定了卡类型/稀有度/牌堆等）时静默放弃——
    /// 附魔迁移失败绝不应阻断英雄技能替换本身。
    /// </summary>
    public static void InheritEnchantment(IReadOnlyList<CardModel> oldCards, CardModel? newCard)
    {
        if (newCard == null || oldCards == null)
        {
            return;
        }
        EnchantmentModel? oldEnchantment = null;
        foreach (var oldCard in oldCards)
        {
            if (oldCard?.Enchantment != null)
            {
                oldEnchantment = oldCard.Enchantment;
                break;
            }
        }
        if (oldEnchantment == null)
        {
            return;
        }
        try
        {
            var clone = (EnchantmentModel)oldEnchantment.MutableClone();
            if (!clone.CanEnchant(newCard))
            {
                return;
            }
            MegaCrit.Sts2.Core.Commands.CardCmd.Enchant(clone, newCard, clone.Amount);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] 英雄技能附魔继承失败(忽略): {ex.Message}");
        }
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
    /// 无限升级卡（点燃/灯光表演）覆写 <see cref="JainaSpellCardTemplate.DiscoverPoolMaxUpgradeLevel"/>
    /// 为 0——发现/随机生成只能获得未升级形态（0 级），
    /// 只有倒带/西瓦拉这类"复制具体施放过的牌"的效果才保留其实际升级层数。
    /// 非吉安娜卡（原版/其他 mod）走原版逻辑：普通升级上限封顶 2 级。
    /// </summary>
    public static int GetDiscoverPoolMaxUpgradeLevel(Type cardType)
    {
        var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(cardType));
        if (canonical == null)
        {
            return 0;
        }
        if (canonical is JainaSpellCardTemplate spellCard)
        {
            return spellCard.DiscoverPoolMaxUpgradeLevel;
        }
        return Math.Min(canonical.MaxUpgradeLevel, 2);
    }

    /// <summary>
    /// 吉安娜卡池（JainaCardPool）全部卡的类型集合（惰性构建；卡池注册冻结后不变）。
    /// </summary>
    private static HashSet<Type>? _jainaPoolCardTypes;

    /// <summary>
    /// 吉安娜中立/衍生池（JainaNeutralCardPool）全部卡的类型集合（惰性构建）。
    /// 含任务奖励卡（时空扭曲/源生之石/奥术师晨拥）与全部衍生牌。
    /// </summary>
    private static HashSet<Type>? _neutralPoolCardTypes;

    /// <summary>
    /// 惰性构建吉安娜两个卡池的类型集合（ModelDb 卡池注册完成后首次调用时构建一次）。
    /// </summary>
    private static void EnsurePoolTypeCache()
    {
        if (_jainaPoolCardTypes != null)
        {
            return;
        }
        var jaina = new HashSet<Type>();
        var neutral = new HashSet<Type>();
        var jainaPool = ModelDb.CardPool<JainaCardPool>();
        if (jainaPool != null)
        {
            foreach (var c in jainaPool.AllCards)
            {
                if (c != null)
                {
                    jaina.Add(c.GetType());
                }
            }
        }
        var neutralPool = ModelDb.CardPool<JainaNeutralCardPool>();
        if (neutralPool != null)
        {
            foreach (var c in neutralPool.AllCards)
            {
                if (c != null)
                {
                    neutral.Add(c.GetType());
                }
            }
        }
        _jainaPoolCardTypes = jaina;
        _neutralPoolCardTypes = neutral;
    }

    /// <summary>
    /// 是否为法术牌（<b>统一判定</b>）：攻击牌/技能牌，或带"法术牌"关键词的<b>能力牌</b>（Power 类型）。
    /// 随从/地标/英雄卡即使带"法术牌"关键词（历史遗留：曾用于悬停解释，现 Spell 为纯内部标记）
    /// 也<b>不</b>算法术牌——防止随从混入法术发现/随机池、被倒带/任务进度/加大音量等误判为法术。
    /// </summary>
    public static bool IsSpellCard(CardModel card)
    {
        if (card == null)
        {
            return false;
        }
        if (card.Type == CardType.Attack || card.Type == CardType.Skill)
        {
            return true;
        }
        return card.Type == CardType.Power &&
               card.Keywords.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Spell);
    }

    /// <summary>
    /// 该类型是否<b>不可</b>被法术发现/随机施放池检索（<b>动态判定，无需手动注册</b>）：
    /// - 任务奖励卡/衍生牌：注册在 JainaNeutralCardPool（时空扭曲/源生之石/奥术师晨拥
    ///   等任务奖励卡与全部衍生牌）——新增任务奖励/衍生卡自动排除；
    /// - 任务卡：带 Quest 关键词（禁忌序列/打开时空之门/巫师的计策/拖延时间/抵达传送大厅）——
    ///   新增任务卡自动排除；
    /// - 吉安娜非法术能力牌：吉安娜卡池（JainaCardPool）中的 Power 类型且不带"法术牌"关键词
    ///   （戏法图腾/炉石形态；寒冰屏障/冰血哨塔带"法术牌"关键词 → 是法术牌，保留；
    ///   原版/其他 mod 的能力牌不在吉安娜卡池 → 保留，惊奇卡牌等全角色池仍可施放原版能力牌）。
    /// 取代原 10 张卡的显式黑名单。
    /// </summary>
    public static bool IsExcludedFromSpellPool(Type type)
    {
        if (type == null)
        {
            return false;
        }
        EnsurePoolTypeCache();
        // 任务奖励卡/衍生牌：JainaNeutralCardPool（新增任务奖励/衍生卡自动排除）
        if (_neutralPoolCardTypes!.Contains(type))
        {
            return true;
        }
        var canonical = ModelDb.GetByIdOrNull<CardModel>(ModelDb.GetId(type));
        if (canonical == null)
        {
            return false;
        }
        // 任务卡：带 Quest 关键词（新增任务卡自动排除）
        if (canonical.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Quest) == true)
        {
            return true;
        }
        // 吉安娜非法术牌：吉安娜卡池中的 Attack/Skill/Power 型<b>且无"法术牌"关键词</b>的卡
        // ——技能型（昔时古树/古拉巴什贡品）、攻击型（火眼莫德雷斯）、能力型（米尔豪斯）均排除;
        // 挂"法术牌"关键词的（火球术/寒冰箭/野火/寒冰屏障等）是法术牌，不在排除范围。
        // 职业：原来只排除 Power 型（戏法图腾/炉石形态），技能型/攻击型的非法术牌
        // 会按"攻击/技能牌视为法术牌"误入法术池/发现（实测：古拉巴什贡品被发现）。
        if (_jainaPoolCardTypes!.Contains(type) &&
            canonical.Type is CardType.Power or CardType.Attack or CardType.Skill &&
            canonical.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Spell) != true)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 动态构建"吉安娜法术牌池"（含升级形态展开）：
    /// 遍历<b>吉安娜卡池</b>（JainaCardPool），取法术牌——攻击/技能牌，
    /// 或带"法术牌"关键词的能力牌（寒冰屏障/冰血哨塔等 Power 型法术，
    /// 与 <see cref="RecordPlayed"/> 的法术判定一致），
    /// 排除英雄技能卡、任务线卡与 <see cref="IsExcludedFromSpellPool"/> 黑名单卡，
    /// 按 <see cref="GetDiscoverPoolMaxUpgradeLevel"/> 展开未升级与升级形态。
    /// 取代各卡硬编码的 typeof 列表（硬编码会漏新增卡/错配升级形态派系）。
    /// 注意：这是<b>吉安娜法术池</b>（唤醒/卡雷苟斯/撕裂现实等发现吉安娜法术用）；
    /// 全角色语义（匣中古神/惊奇卡牌/戏法图腾等卡面写明"全角色卡牌"的）不走此方法。
    /// 只返回 (类型, 级别) 候选列表，不创建实例——无需战斗状态/玩家参数。
    /// </summary>
    public static List<(Type Type, int UpgradeLevel)> BuildAllSpellPool()
    {
        var result = new List<(Type, int)>();
        foreach (var canonical in ModelDb.CardPool<JainaCardPool>().AllCards)
        {
            if (canonical == null)
            {
                continue;
            }
            // 法术牌 = 统一判定（攻击/技能，或带"法术牌"关键词的能力牌；随从/地标不算）
            bool isSpellCard = IsSpellCard(canonical);
            if (!isSpellCard)
            {
                continue;
            }
            // 显式黑名单：戏法图腾/炉石形态不是法术牌；禁忌序列/打开时空之门任务卡不可被发现
            if (IsExcludedFromSpellPool(canonical.GetType()))
            {
                continue;
            }
            // 随机池统一排除（先古稀有度——米尔豪斯·法力风暴等、多人专属卡）：
            // 先古卡不可被唤醒/发现等效果开出来
            if (!jaina.Scripts.Character.JainaRandomPoolHelper.IsEligible(canonical))
            {
                continue;
            }
            if (Powers.HeroPowerHandHelper.IsHeroPowerCard(canonical))
            {
                continue;
            }
            if (canonical.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Quest) == true)
            {
                continue;
            }
            int maxLevel = GetDiscoverPoolMaxUpgradeLevel(canonical.GetType());
            for (int level = 0; level <= maxLevel; level++)
            {
                result.Add((canonical.GetType(), level));
            }
        }
        return result;
    }

    /// <summary>
    /// 动态构建指定派系的法术牌池（火焰/冰霜/奥术）：<b>吉安娜卡池</b>（JainaCardPool）
    /// 的攻击/技能牌中，按<b>每个形态实例</b>的本地派系关键词过滤
    /// （升级后派系变化的形态自动排除），排除英雄技能卡、任务线卡与指定排除类型
    /// （同名不可自发现）。取代各卡硬编码的 typeof 派系列表。
    /// </summary>
    public static List<(Type Type, int UpgradeLevel)> BuildSchoolSpellPool(
        ICombatState combatState, Player owner, JainaSpellSchool school, Type? excludeType = null)
    {
        CardKeyword? keyword = school switch
        {
            JainaSpellSchool.Fire => jaina.Scripts.Character.Keywords.JainaKeywords.Fire,
            JainaSpellSchool.Frost => jaina.Scripts.Character.Keywords.JainaKeywords.Frost,
            JainaSpellSchool.Arcane => jaina.Scripts.Character.Keywords.JainaKeywords.Arcane,
            _ => null
        };
        if (keyword == null)
        {
            return [];
        }
        var result = new List<(Type, int)>();
        foreach (var canonical in ModelDb.CardPool<JainaCardPool>().AllCards)
        {
            if (canonical == null)
            {
                continue;
            }
            // 法术牌 = 统一判定（攻击/技能，或带"法术牌"关键词的能力牌；随从/地标不算）
            bool isSpellCard = IsSpellCard(canonical);
            if (!isSpellCard)
            {
                continue;
            }
            // 显式黑名单：戏法图腾/炉石形态不是法术牌；禁忌序列/打开时空之门任务卡不可被发现
            if (IsExcludedFromSpellPool(canonical.GetType()))
            {
                continue;
            }
            // 随机池统一排除（先古稀有度——米尔豪斯·法力风暴等、多人专属卡）：
            // 先古卡不可被发现/唤醒等效果开出来
            if (!jaina.Scripts.Character.JainaRandomPoolHelper.IsEligible(canonical))
            {
                continue;
            }
            if (Powers.HeroPowerHandHelper.IsHeroPowerCard(canonical))
            {
                continue;
            }
            // 同名卡不可自发现：排除发起发现的卡自身
            if (excludeType != null && canonical.GetType() == excludeType)
            {
                continue;
            }
            if (canonical.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Quest) == true)
            {
                continue;
            }
            int maxLevel = GetDiscoverPoolMaxUpgradeLevel(canonical.GetType());
            for (int level = 0; level <= maxLevel; level++)
            {
                var card = CreateCardWithUpgrade(combatState, owner, canonical.GetType(), level);
                // 该形态实例挂对应派系关键词才纳入（升级后派系变化的形态自动排除）
                // keyword 非空（上面已 return）
                if (card != null && keyword != null &&
                    card.GetKeywordsWithSources(MegaCrit.Sts2.Core.Entities.Cards.KeywordSources.Local)
                        .Contains(keyword.Value))
                {
                    result.Add((canonical.GetType(), level));
                }
            }
        }
        return result;
    }
}
