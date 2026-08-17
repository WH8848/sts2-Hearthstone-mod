using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 联机确定性修复：SavedProperty 属性 net-id 分配确定性化。
///
/// 问题：游戏核心 ModelIdSerializationCache.Init 按"模型遍历顺序"（ContentSorter 排序结果）
/// 逐个注册属性的 net-id——两端安装的 mod 集合/顺序不同 → 模型遍历顺序不同 →
/// 同一属性（如 CurrentBlock、CardsPlayed）在两端被分配了不同的 net-id →
/// 联机时按 net-id 序列化/反序列化卡牌与玩家状态 → 同一状态两端字节不同 →
/// checksum 不匹配 → State Divergence 断线。
///
/// 修复：在 Init 之前，收集全部模型的 SavedProperty 属性，
/// 按 (order, 属性名) 全局确定性排序后预注册——属性 net-id 与模型遍历顺序无关，
/// 相同属性名在两端总是得到相同 net-id（两端属性名集合一致）。
/// 原版 CachePropertiesForType 的注册循环对已存在名称跳过，行为不受影响。
/// </summary>
[HarmonyPatch(typeof(ModelIdSerializationCache), "Init")]
public static class DeterministicSavedPropertyNetIdPatch
{
    public static void Prefix()
    {
        try
        {
            var mapField = AccessTools.Field(typeof(ModelIdSerializationCache), "_propertyNameToNetIdMap");
            var listField = AccessTools.Field(typeof(ModelIdSerializationCache), "_netIdToPropertyNameMap");
            if (mapField == null || listField == null)
            {
                return;
            }
            var nameToNetId = (Dictionary<string, int>)mapField.GetValue(null);
            var netIdToName = (List<string>)listField.GetValue(null);

            // 收集全部模型的 SavedProperty 属性（order, 名称）
            var props = new List<(int Order, string Name)>();
            foreach (var type in ModelDb.All.Select(m => m.GetType()).Distinct())
            {
                foreach (var p in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var attr = p.GetCustomAttribute<SavedPropertyAttribute>();
                    if (attr != null)
                    {
                        props.Add((attr.order, p.Name));
                    }
                }
            }

            // 全局确定性排序（与 CachePropertiesForType 的 CompareProperties 一致：
            // 先 order，再属性名 Ordinal）——与模型遍历顺序无关
            foreach (var (order, name) in props
                         .OrderBy(x => x.Order)
                         .ThenBy(x => x.Name, StringComparer.Ordinal))
            {
                if (!nameToNetId.ContainsKey(name))
                {
                    nameToNetId[name] = netIdToName.Count;
                    netIdToName.Add(name);
                }
            }
        }
        catch
        {
            // 预注册失败不阻塞游戏（退回原逻辑）
        }
    }
}
