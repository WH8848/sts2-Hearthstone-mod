using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace jaina.Scripts.Character.Cards;

/// <summary>
/// "先古卡样式"开关的运行时状态与持久化。
/// 布局切换走 RitsuLib 的 ModCardTemplate.CustomVisualStyle（virtual，不 patch）；
/// 卡图路径切换走 JainaAncientPortraitPatch（每张卡各自覆写 CustomPortraitPath，无法在基类统一拦截）。
/// 证据：CardVisualStyle 枚举 ContentAssetProfiles.cs:204；ModCardTemplate.CustomVisualStyle
/// ModCardTemplate.cs:101。
/// </summary>
public static class JainaCardStyle
{
    private const string SettingsPath = "user://jaina_settings.cfg";
    private const string Section = "card_style";

    private static bool _ancient;
    private static bool _loaded;

    /// <summary>先古卡样式是否开启。</summary>
    public static bool Ancient
    {
        get
        {
            EnsureLoaded();
            return _ancient;
        }
        set
        {
            EnsureLoaded();
            _ancient = value;
        }
    }

    /// <summary>持久化当前开关状态（RitsuLib 设置页的 save 回调调用）。</summary>
    public static void Save()
    {
        try
        {
            var cfg = new ConfigFile();
            cfg.SetValue(Section, "ancient_card_style", _ancient);
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
            }
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[Jaina] load card style failed: {ex.Message}");
        }
    }

    /// <summary>把 assets/card_art/ 路径换成 assets/card_art_ancient/（文件不存在则返回原路径）。</summary>
    public static string ToAncientPath(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.Contains("/assets/card_art/"))
        {
            return path;
        }
        var candidate = path.Replace("/assets/card_art/", "/assets/card_art_ancient/");
        return ResourceLoader.Exists(candidate) ? candidate : path;
    }
}
