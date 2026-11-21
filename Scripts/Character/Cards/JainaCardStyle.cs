using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>卡图风格（四选一，持久化在 user://jaina_settings.cfg）。</summary>
public enum JainaCardStyleMode
{
    /// <summary>炉石传说风格（默认）：原画横版 250x190 + 普通布局。</summary>
    HearthstoneDefault = 0,

    /// <summary>炉石传说风格（先古）：原画竖版 250x351 + 先古布局。</summary>
    HearthstoneAncient = 1,

    /// <summary>杀戮尖塔风格（默认）：手绘横版 250x190 + 普通布局。</summary>
    StsDefault = 2,

    /// <summary>杀戮尖塔风格（先古）：手绘竖版 250x351 + 先古布局。</summary>
    StsAncient = 3,
}

/// <summary>
/// 卡图风格选择（单选）的运行时状态与持久化。
/// 布局切换走 RitsuLib 的 ModCardTemplate.CustomVisualStyle（virtual，不 patch）；
/// 卡图路径切换走 JainaAncientPortraitPatch（每张卡各自覆写 CustomPortraitPath，无法在基类统一拦截）。
/// 证据：CardVisualStyle 枚举 ContentAssetProfiles.cs:204；ModCardTemplate.CustomVisualStyle
/// ModCardTemplate.cs:101。
/// </summary>
public static class JainaCardStyle
{
    private const string SettingsPath = "user://jaina_settings.cfg";
    private const string Section = "card_style";
    private const string ArtRoot = "/assets/card_art/";

    private static JainaCardStyleMode _mode = JainaCardStyleMode.HearthstoneDefault;
    private static bool _loaded;

    /// <summary>当前卡图风格（四选一）。</summary>
    public static JainaCardStyleMode Mode
    {
        get { EnsureLoaded(); return _mode; }
        set
        {
            EnsureLoaded();
            if (_mode == value) return;
            _mode = value;
            Save();
            RefreshCards();
        }
    }

    /// <summary>卡面是否用先古布局（炉石先古 或 尖塔先古）。</summary>
    public static bool UseAncientLayout =>
        Mode is JainaCardStyleMode.HearthstoneAncient or JainaCardStyleMode.StsAncient;

    /// <summary>持久化当前选择。</summary>
    public static void Save()
    {
        try
        {
            var cfg = new ConfigFile();
            cfg.SetValue(Section, "card_art_style", (int)Mode);
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
                var v = cfg.GetValue(Section, "card_art_style", 0).AsInt32();
                if (v is >= 0 and <= 3)
                {
                    _mode = (JainaCardStyleMode)v;
                }
            }
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] load card style failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 按当前风格解析卡图路径，文件不存在时逐级回退：
    /// 尖塔先古：stylized_ancient → stylized → ancient → 原路径
    /// 尖塔默认：stylized → 原路径
    /// 炉石先古：ancient → 原路径
    /// 炉石默认：原路径
    /// </summary>
    public static string ResolvePortrait(string path, bool ancientCard = false)
    {
        if (string.IsNullOrEmpty(path) || !path.Contains(ArtRoot))
        {
            return path;
        }

        // 先古卡（CardRarity.Ancient，如血望者/二级火焰冲击）永远使用 250x351 竖版目录，
        // 不经过普通卡目录——游戏里它们只走 %AncientPortrait（NCard.SetPortraitTexture:847-861）。
        if (ancientCard)
        {
            // 先古卡只存在于 *_ancient 目录（250x351），按文件名重组路径，不依赖原目录
            var fileName = path.Substring(path.LastIndexOf('/') + 1);
            var modernAncient = Mode is JainaCardStyleMode.StsDefault or JainaCardStyleMode.StsAncient;
            var ancientDir = modernAncient ? "card_art_stylized_ancient" : "card_art_ancient";
            var ancientCandidate = $"res://assets/{ancientDir}/{fileName}";
            if (ResourceLoader.Exists(ancientCandidate))
            {
                return ancientCandidate;
            }
            // 尖塔风格缺图时回退到炉石原画竖版
            var fallback = $"res://assets/card_art_ancient/{fileName}";
            if (ResourceLoader.Exists(fallback))
            {
                return fallback;
            }
            return path;
        }

        string[] dirs = Mode switch
        {
            JainaCardStyleMode.StsAncient => ["card_art_stylized_ancient", "card_art_stylized", "card_art_ancient"],
            JainaCardStyleMode.StsDefault => ["card_art_stylized"],
            JainaCardStyleMode.HearthstoneAncient => ["card_art_ancient"],
            _ => [],
        };

        foreach (var dir in dirs)
        {
            var candidate = path.Replace(ArtRoot, $"/assets/{dir}/");
            if (ResourceLoader.Exists(candidate))
            {
                return candidate;
            }
        }

        return path;
    }

    /// <summary>
    /// 风格切换后强制刷新场上所有卡面（否则已渲染的 NCard 会继续用旧纹理）。
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
}
