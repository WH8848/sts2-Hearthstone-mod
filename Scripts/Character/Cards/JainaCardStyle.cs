using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// 卡面样式开关的运行时状态与持久化：
/// ① 先古卡样式（Ancient）：布局切换走 RitsuLib 的 ModCardTemplate.CustomVisualStyle（virtual，不 patch），
///    卡图切到 250x351 竖版目录；
/// ② 杀戮尖塔二风格卡图（Stylized）：卡图换成 assets/card_art_stylized/ 下的手绘风格版。
/// 卡图路径切换走 JainaAncientPortraitPatch（每张卡各自覆写 CustomPortraitPath，无法在基类统一拦截）。
/// 证据：CardVisualStyle 枚举 ContentAssetProfiles.cs:204；ModCardTemplate.CustomVisualStyle
/// ModCardTemplate.cs:101。
/// </summary>
public static class JainaCardStyle
{
    private const string SettingsPath = "user://jaina_settings.cfg";
    private const string Section = "card_style";
    private const string ArtRoot = "/assets/card_art/";

    private static bool _ancient;
    private static bool _stylized;
    private static bool _loaded;

    /// <summary>先古卡样式是否开启（卡面布局 + 竖版卡图）。</summary>
    public static bool Ancient
    {
        get { EnsureLoaded(); return _ancient; }
        set
        {
            EnsureLoaded();
            if (_ancient == value) return;
            _ancient = value;
            RefreshCards();
        }
    }

    /// <summary>杀戮尖塔二风格卡图是否开启（替换卡图内容，不改布局）。</summary>
    public static bool Stylized
    {
        get { EnsureLoaded(); return _stylized; }
        set
        {
            EnsureLoaded();
            if (_stylized == value) return;
            _stylized = value;
            RefreshCards();
        }
    }

    /// <summary>持久化当前开关状态（RitsuLib 设置页的 save 回调调用）。</summary>
    public static void Save()
    {
        try
        {
            var cfg = new ConfigFile();
            cfg.SetValue(Section, "ancient_card_style", _ancient);
            cfg.SetValue(Section, "stylized_card_art", _stylized);
            cfg.Save(SettingsPath);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] save card style failed: {ex.Message}");
        }
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }
        _loaded = true;
        try
        {
            var cfg = new ConfigFile();
            if (cfg.Load(SettingsPath) == Error.Ok)
            {
                _ancient = cfg.GetValue(Section, "ancient_card_style", false).AsBool();
                _stylized = cfg.GetValue(Section, "stylized_card_art", false).AsBool();
            }
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] load card style failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 开关变化后强制刷新场上所有卡面（否则已渲染的 NCard 会继续用旧纹理）。
    /// 主菜单没有卡面节点时是空操作。
    /// </summary>
    private static void RefreshCards()
    {
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            var root = tree?.Root;
            if (root == null)
            {
                return;
            }
            RefreshNode(root);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] refresh cards failed: {ex.Message}");
        }
    }

    private static readonly System.Reflection.MethodInfo? ReloadMethod =
        typeof(MegaCrit.Sts2.Core.Nodes.Cards.NCard).GetMethod(
            "Reload",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    private static void RefreshNode(Node node)
    {
        if (node is MegaCrit.Sts2.Core.Nodes.Cards.NCard card)
        {
            // NCard.Reload 是 private（0.113 src/Core/Nodes/Cards/NCard.cs:728），只能反射调用
            ReloadMethod?.Invoke(card, null);
        }
        foreach (var child in node.GetChildren())
        {
            RefreshNode(child);
        }
    }

    /// <summary>
    /// 按当前开关解析卡图路径。回退顺序：
    /// 先古布局：card_art_stylized_ancient → card_art_stylized → card_art_ancient → 原路径
    /// 普通布局：card_art_stylized → 原路径
    /// 文件不存在时逐级回退，最终返回原路径。
    /// </summary>
    public static string ResolvePortrait(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.Contains(ArtRoot))
        {
            return path;
        }

        if (Stylized)
        {
            var dirs = Ancient
                ? new[] { "card_art_stylized_ancient", "card_art_stylized", "card_art_ancient" }
                : new[] { "card_art_stylized" };
            foreach (var dir in dirs)
            {
                var candidate = path.Replace(ArtRoot, $"/assets/{dir}/");
                if (ResourceLoader.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        if (Ancient)
        {
            var candidate = path.Replace(ArtRoot, "/assets/card_art_ancient/");
            if (ResourceLoader.Exists(candidate))
            {
                return candidate;
            }
        }

        return path;
    }
}
