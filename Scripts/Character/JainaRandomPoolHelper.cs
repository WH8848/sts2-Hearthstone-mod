using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace jaina.Scripts.Character;

/// <summary>
/// 随机卡牌池排除标记接口：实现此接口的<b>卡池</b>（如吉安娜中立/衍生池
/// JainaNeutralCardPool——含任务奖励卡与全部衍生牌）被 <see cref="JainaRandomPoolHelper"/>
/// 排除——其中的卡不可被发现、不可被随机释放/随机生成。
/// <b>新增需排除的中性/衍生池实现此接口即可自动排除</b>（反射收集），
/// 无需手动维护排除列表。
/// </summary>
public interface IJainaExcludedFromRandomPool
{
}

/// <summary>
/// Jaina 随机卡牌池通用过滤（所有随机取卡统一使用）：
/// 排除 7 个原版非角色/衍生卡池（无色/诅咒/先古/状态/任务/事件/衍生）+
/// 实现 <see cref="IJainaExcludedFromRandomPool"/> 的 mod 池（吉安娜中立衍生池
/// JainaNeutralCardPool——含任务奖励卡与全部衍生牌，接口动态收集）、
/// 先古稀有度（CardRarity.Ancient）、多人游戏专属卡（MultiplayerConstraint != None）
/// 与<b>任务卡</b>（带 Quest 关键词的卡，如禁忌序列/打开时空之门/巫师的计策/拖延时间/抵达传送大厅——
/// 不可被发现、不可被随机释放/随机生成）。
/// 应用于：匣中古神/谜之匣、惊奇卡牌、戏法图腾、能量塑形师、惊奇套牌、旅社谍战等
/// 从 ModelDb.AllCards / AllCharacterCardPools 随机取卡的所有位置。
/// 另提供随机施放的目标放宽：AnyEnemy 单体攻击牌除非描述限定"对敌人"，
/// 否则随机施放时目标放宽为全部存活生物（自己/队友/双方随从/敌人）。
/// </summary>
public static class JainaRandomPoolHelper
{
    /// <summary>
    /// 被排除的原版非角色卡池类型
    /// （无色/诅咒/先古/状态/任务/事件/衍生池——游戏本体固定池，无动态化手段）。
    /// mod 侧需排除的池（JainaNeutralCardPool 等）走 <see cref="IJainaExcludedFromRandomPool"/>
    /// 接口动态收集，不在此列表。
    /// </summary>
    private static readonly HashSet<Type> ExcludedPoolTypes =
    [
        typeof(MegaCrit.Sts2.Core.Models.CardPools.ColorlessCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.CurseCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.DeprecatedCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.StatusCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.QuestCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.EventCardPool),
        typeof(MegaCrit.Sts2.Core.Models.CardPools.TokenCardPool)
    ];

    /// <summary>
    /// 实现 <see cref="IJainaExcludedFromRandomPool"/> 的 mod 池类型集合
    /// （惰性构建：遍历 ModelDb.AllCardPools 收集实现接口的池；卡池注册冻结后不变）。
    /// 新增需排除的中性/衍生池实现接口即自动加入。
    /// </summary>
    private static HashSet<Type>? _modExcludedPoolTypes;

    private static void EnsureModExcludedPoolTypes()
    {
        if (_modExcludedPoolTypes != null)
        {
            return;
        }
        var set = new HashSet<Type>();
        foreach (var pool in ModelDb.AllCardPools)
        {
            if (pool == null)
            {
                continue;
            }
            if (typeof(IJainaExcludedFromRandomPool).IsAssignableFrom(pool.GetType()))
            {
                set.Add(pool.GetType());
            }
        }
        _modExcludedPoolTypes = set;
    }

    /// <summary>
    /// 该 canonical 卡是否可进入 Jaina 随机卡牌池：
    /// 不属于 7 个非角色池、不是先古稀有度、不是多人游戏专属卡、
    /// 不是任务卡（带 Quest 关键词——不可被随机释放/随机生成）。
    /// </summary>
    public static bool IsEligible(CardModel? canonical)
    {
        if (canonical == null)
        {
            return false;
        }
        if (IsInExcludedPool(canonical))
        {
            return false;
        }
        if (canonical.Rarity == CardRarity.Ancient)
        {
            return false;
        }
        if (canonical.MultiplayerConstraint != CardMultiplayerConstraint.None)
        {
            return false;
        }
        // 任务卡（禁忌序列/打开时空之门/巫师的计策/拖延时间/抵达传送大厅等带 Quest 关键词的卡）
        // 不可被随机释放/随机生成
        if (canonical.CanonicalKeywords?.Contains(jaina.Scripts.Character.Keywords.JainaKeywords.Quest) == true)
        {
            return false;
        }
        return true;
    }

    // ==================== 随机施放目标放宽 ====================

    /// <summary>
    /// 描述中明确限定"对敌人"的卡牌 Id.Entry 集合（预计算缓存）。
    /// 判断固定读 zhs 本地化（游戏 + 本 mod 两个文件）——与运行语言无关，联机两端一致。
    /// </summary>
    private static readonly HashSet<string> EnemyLimitedEntries = new HashSet<string>(StringComparer.Ordinal);

    private static bool _enemyLimitedCacheLoaded;

    /// <summary>
    /// 该 AnyEnemy 单体攻击牌是否描述限定"对敌人"（是 → 随机施放保持敌人目标；
    /// 否 → 放宽为全部存活生物）。非攻击牌/非 AnyEnemy 卡返回 false（不涉及放宽判定）。
    /// </summary>
    public static bool IsEnemyLimitedAttack(CardModel? canonical)
    {
        if (canonical == null || canonical.Type != CardType.Attack || canonical.TargetType != TargetType.AnyEnemy)
        {
            return false;
        }
        EnsureEnemyLimitedCache();
        return EnemyLimitedEntries.Contains(canonical.Id.Entry);
    }

    /// <summary>
    /// 随机施放目标选择（匣中古神/谜之匣、惊奇卡牌、戏法图腾、诈骗犯重放统一使用）：
    /// - 无目标卡（TargetType.None）→ 返回 null；
    /// - AnyEnemy 单体攻击牌且描述未限定"对敌人" → 目标放宽为全部存活生物
    ///   （自己/队友角色、双方随从、敌人——像火球术一样可打任意活物）；
    /// - 其余按卡自身合法性过滤（合法目标优先；自定义目标类型无法判定时回退全部活物，
    ///   保证卡牌总能施放，联机可打队友）。
    /// </summary>
    public static Creature? PickRandomTarget(Player owner, ICombatState combatState, CardModel card)
    {
        if (card == null || card.TargetType == TargetType.None)
        {
            return null;
        }
        var rng = owner.RunState.Rng.CombatTargets;
        var allCreatures = combatState.Creatures
            .Concat(combatState.Players.SelectMany(p => p.PlayerCombatState?.Pets ?? []))
            .Where(c => c != null && c.IsAlive)
            .ToList();
        if (allCreatures.Count == 0)
        {
            return null;
        }
        IEnumerable<Creature> pool;
        if (card.TargetType == TargetType.AnyEnemy && !IsEnemyLimitedAttack(card))
        {
            // 原版攻击牌（描述未限定"对敌人"）：目标放宽为全部存活生物
            pool = allCreatures;
        }
        else
        {
            var legal = allCreatures.Where(c => card.IsValidTarget(c)).ToList();
            pool = legal.Count > 0 ? legal : allCreatures;
        }
        return rng.NextItem(pool);
    }

    /// <summary>
    /// 惰性加载"描述限定敌人"缓存：读取 res://localization/zhs/cards.json（游戏原版）
    /// 与 res://jaina/localization/zhs/cards.json（本 mod），收集所有描述含"敌人"的卡 Id.Entry。
    /// 固定语言判断 → 与玩家运行语言无关，联机两端结果一致（确定性）。
    /// </summary>
    private static void EnsureEnemyLimitedCache()
    {
        if (_enemyLimitedCacheLoaded)
        {
            return;
        }
        _enemyLimitedCacheLoaded = true;
        try
        {
            CollectEnemyLimitedFromFile("res://localization/zhs/cards.json");
            CollectEnemyLimitedFromFile("res://jaina/localization/zhs/cards.json");
        }
        catch (Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"[Jaina] enemy-limited cache failed: {ex}");
        }
    }

    private static void CollectEnemyLimitedFromFile(string path)
    {
        if (!Godot.FileAccess.FileExists(path))
        {
            return;
        }
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return;
        }
        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            return;
        }
        var dict = parsed.AsGodotDictionary();
        const string suffix = ".description";
        foreach (var key in dict.Keys)
        {
            var keyStr = key.AsString();
            if (!keyStr.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }
            var text = dict[key].AsString();
            if (text.Contains("敌人", StringComparison.Ordinal))
            {
                EnemyLimitedEntries.Add(keyStr.Substring(0, keyStr.Length - suffix.Length));
            }
        }
    }

    private static bool IsInExcludedPool(CardModel canonical)
    {
        EnsureModExcludedPoolTypes();
        foreach (var pool in ModelDb.AllCardPools)
        {
            if (pool == null)
            {
                continue;
            }
            var poolType = pool.GetType();
            // 原版非角色池（硬编码，本体固定）或 mod 排除池（接口动态收集）
            if (!ExcludedPoolTypes.Contains(poolType) && !_modExcludedPoolTypes!.Contains(poolType))
            {
                continue;
            }
            if (pool.AllCards.Contains(canonical))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 随机释放"不可释放自身"规则：正在随机释放的法术的池中排除<b>自身 canonical 类型</b>
    /// （按类型而非实例：随机池由 canonical 展开的新实例构建，无法按实例匹配）。
    /// 例：匣中古神不可随机释放匣中古神；但匣中古神随机释放出的大法师的符文，
    /// 该符文随机释放时排除的是"大法师的符文"类型——匣中古神仍可被其释放。
    /// </summary>
    /// <param name="canonical">池中的候选 canonical 卡。</param>
    /// <param name="releaseSourceType">当前正在随机释放的那张卡的 canonical 类型。</param>
    public static bool IsRandomReleaseSelf(CardModel canonical, System.Type releaseSourceType)
    {
        return canonical != null && canonical.GetType() == releaseSourceType;
    }
}
