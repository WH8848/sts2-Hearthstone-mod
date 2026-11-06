using System;
using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;

namespace jaina.Scripts;

/// <summary>
/// 游戏反射访问统一入口（A2 优化）：
/// 缓存 Field/Method 查找结果，缺失时打一次 Warn——游戏版本更新导致私有成员改名时，
/// 日志会明确提示哪个类型/成员失效，而不是静默返回 null。
/// </summary>
public static class GameReflection
{
    private static readonly ConcurrentDictionary<(Type Type, string Name), FieldInfo?> Fields = new();
    private static readonly ConcurrentDictionary<(Type Type, string Name), MethodInfo?> Methods = new();

    /// <summary>取私有/公有字段（带缓存与缺失告警）。</summary>
    public static FieldInfo? Field(Type type, string name)
    {
        return Fields.GetOrAdd((type, name), key =>
        {
            var field = AccessTools.Field(key.Type, key.Name);
            if (field == null)
            {
                MegaCrit.Sts2.Core.Logging.Log.Warn(
                    $"[Jaina] reflection: {key.Type.Name}.{key.Name} field not found (game update?)");
            }
            return field;
        });
    }

    /// <summary>取方法（带缓存与缺失告警）。</summary>
    public static MethodInfo? Method(Type type, string name, params Type[]? parameters)
    {
        return Methods.GetOrAdd((type, name), key =>
        {
            var method = parameters is { Length: > 0 }
                ? AccessTools.Method(key.Type, key.Name, parameters)
                : AccessTools.Method(key.Type, key.Name);
            if (method == null)
            {
                MegaCrit.Sts2.Core.Logging.Log.Warn(
                    $"[Jaina] reflection: {key.Type.Name}.{key.Name}() method not found (game update?)");
            }
            return method;
        });
    }
}
