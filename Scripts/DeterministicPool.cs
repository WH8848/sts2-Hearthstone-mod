using System;
using System.Collections.Generic;

namespace jaina.Scripts;

/// <summary>
/// 确定性随机池工具（联机 B 类）：
/// 随机池候选表如果来自 Dictionary/HashSet 的迭代顺序（依赖 Type 哈希，跨进程不保证一致），
/// 同一个 RNG 索引会选出不同元素 → 两端状态分歧。凡随机池喂给 NextItem 前，用这里的方法
/// 按类型全名 ordinal 排序，保证两端候选表完全一致。
/// List/数组等顺序确定的池无需调用。
/// </summary>
public static class DeterministicPool
{
    /// <summary>按类型全名（ordinal）排序 Type 候选表。</summary>
    public static void SortByTypeName(List<Type> candidates)
    {
        if (candidates == null || candidates.Count <= 1)
        {
            return;
        }
        candidates.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
    }

    /// <summary>按类型全名（ordinal）排序 (Type, int) 候选表（用于含升级形态的池）。</summary>
    public static void SortByTypeName(List<(Type Type, int UpgradeLevel)> candidates)
    {
        if (candidates == null || candidates.Count <= 1)
        {
            return;
        }
        candidates.Sort((a, b) =>
        {
            int byName = string.CompareOrdinal(a.Type?.FullName, b.Type?.FullName);
            return byName != 0 ? byName : a.UpgradeLevel.CompareTo(b.UpgradeLevel);
        });
    }
}
