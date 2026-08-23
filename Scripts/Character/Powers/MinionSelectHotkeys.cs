using System.Collections.Generic;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using jaina.Scripts.Character.Minions;
using STS2RitsuLib.RuntimeInput;
using STS2RitsuLib.Settings;

namespace jaina.Scripts.Character.Powers;

/// <summary>
/// 随从选中快捷键（基于 RitsuLib RuntimeHotkeyService 运行时热键体系）：
/// 默认小键盘 1-7 选中/取消选中己方第 1-7 个随从（金色选中框 + 随从卡卡面），
/// 默认 Esc 取消当前选中。再按一次同一数字 = 取消。
/// <para>
/// 更改快捷键的方式：RitsuLib 设置中心 → Jaina →「随从选中快捷键」页面，
/// 每一行都是 RitsuLib 自带键位录制控件（AddKeyBinding：点击录制 / Clear /
/// Reset to default），修改即时生效并持久化到 user://jaina_minion_select_hotkeys.json；
/// 同时热键注册带 Id/DisplayName/Description/Category，会自动出现在 RitsuLib
/// 的「运行时快捷键」总览页（按类别分组只读展示）。
/// </para>
/// 纯本地 UI 功能（选中仅影响本端显示），联机安全；战斗开始/结束自动清理选中状态。
/// </summary>
public static class MinionSelectHotkeys
{
    /// <summary>可选的随从数量（小键盘 1-7）</summary>
    public const int MinionCount = 7;

    /// <summary>取消快捷键的槽位键</summary>
    private const string CancelSlot = "cancel";

    private const string StoragePath = "user://jaina_minion_select_hotkeys.json";
    private const string DefaultCancelBinding = "Escape";

    private static readonly Dictionary<string, string> Persisted = [];
    private static readonly Dictionary<int, IRuntimeHotkeyHandle> SelectHandles = [];
    private static IRuntimeHotkeyHandle? _cancelHandle;
    private static CombatState? _state;
    private static bool _initialized;
    private static bool _settingsPageRegistered;
    private static JainaMinionBase? _selected;

    /// <summary>当前被选中的随从（null = 未选中），供调试/扩展使用</summary>
    public static JainaMinionBase? SelectedMinion => _selected;

    /// <summary>
    /// 注册 7 个选中热键 + 1 个取消热键（幂等），并订阅战斗开始/结束清空选中状态。
    /// Entry.Init 调用；RitsuLib RuntimeHotkeyService 首次 Register 自动初始化。
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        LoadPersisted();

        // 战斗生命周期：开始缓存状态，结束清空选中（防止战斗结束残留选中框）
        CombatManager.Instance.CombatBegan += state => _state = state;
        CombatManager.Instance.CombatEnded += _ =>
        {
            _state = null;
            Deselect();
        };

        for (var i = 1; i <= MinionCount; i++)
        {
            var index = i;
            var slot = index.ToString();
            SelectHandles[index] = RuntimeHotkeyService.Register(
                GetBinding(slot),
                () => SelectByIndex(index),
                new RuntimeHotkeyOptions
                {
                    Id = IdFor(index),
                    DisplayName = RuntimeHotkeyText.Dynamic(
                        () => Loc($"JAINA_UI_MINION_SELECT_{index}", $"Select minion {index}")),
                    Description = RuntimeHotkeyText.Dynamic(() =>
                        Loc($"JAINA_UI_MINION_SELECT_{index}_DESC",
                            $"Toggle selection of your {index}th minion.")),
                    Category = RuntimeHotkeyText.Dynamic(
                        () => Loc("JAINA_UI_MINION_SELECT_CATEGORY", "Jaina minion select")),
                    MarkInputHandled = true,
                    DebugName = $"Jaina minion select {index}",
                });
        }

        _cancelHandle = RuntimeHotkeyService.Register(
            GetBinding(CancelSlot),
            Deselect,
            new RuntimeHotkeyOptions
            {
                Id = $"{Entry.ModId}.minion_select.cancel",
                DisplayName = RuntimeHotkeyText.Dynamic(
                    () => Loc("JAINA_UI_MINION_SELECT_CANCEL", "Cancel selection")),
                Description = RuntimeHotkeyText.Dynamic(
                    () => Loc("JAINA_UI_MINION_SELECT_CANCEL_DESC", "Deselect the currently selected minion.")),
                Category = RuntimeHotkeyText.Dynamic(
                    () => Loc("JAINA_UI_MINION_SELECT_CATEGORY", "Jaina minion select")),
                MarkInputHandled = true,
                DebugName = "Jaina minion select cancel",
            });

        RegisterModSettingsPage();
    }

    /// <summary>
    /// 随从死亡时通知（JainaMinionBase.AfterDeath 调用）：若正被选中则取消选中。
    /// </summary>
    public static void NotifyMinionDied(JainaMinionBase minion)
    {
        if (_selected == minion)
        {
            Deselect();
        }
    }

    /// <summary>
    /// 按编号选中/取消选中己方第 <paramref name="index"/> 个随从（再按一次 = 取消）。
    /// </summary>
    private static void SelectByIndex(int index)
    {
        try
        {
            var state = _state;
            if (state == null)
            {
                return;
            }
            var player = LocalContext.GetMe(state);
            var minions = player == null
                ? []
                : EnumerateOwnMinions(player);
            if (index > minions.Count)
            {
                return;
            }
            var target = minions[index - 1];
            if (target == null)
            {
                return;
            }
            MegaCrit.Sts2.Core.Logging.Log.Info(
                $"[JainaSelect] Numpad{index} -> {target.GetType().Name} pets={minions.Count} state={(state == null ? "null" : "ok")} side={player.Creature.Side}");
            if (player.Creature.CombatState != null &&
                player.Creature.CombatState.CurrentSide != player.Creature.Side)
            {
                MegaCrit.Sts2.Core.Logging.Log.Info(
                    $"[JainaSelect] note: not player side (current={player.Creature.CombatState.CurrentSide}), click-to-attack will be ignored by MinionLib");
            }
            if (_selected == target)
            {
                Deselect();
                return;
            }
            Deselect();
            _selected = target;
            target.SetHotkeySelected(true);
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[JainaSelect] select error: {ex.Message}");
        }
    }

    /// <summary>
    /// 取消当前选中（清选中框/卡面）。
    /// </summary>
    private static void Deselect()
    {
        var prev = _selected;
        _selected = null;
        prev?.SetHotkeySelected(false);
    }

    /// <summary>
    /// 本地玩家自己的 Jaina 随从列表（宠物顺序 = 召唤顺序）。
    /// </summary>
    private static List<JainaMinionBase> EnumerateOwnMinions(Player player)
    {
        var result = new List<JainaMinionBase>();
        var pets = player.PlayerCombatState?.Pets;
        if (pets == null)
        {
            return result;
        }
        foreach (var pet in pets)
        {
            if (pet == null || !pet.IsAlive)
            {
                continue;
            }
            if (pet.Monster is JainaMinionBase minion && minion.Creature == pet)
            {
                result.Add(minion);
            }
        }
        return result;
    }

    /// <summary>
    /// 当前生效的绑定（持久化覆盖优先，否则默认）。
    /// </summary>
    private static string GetBinding(string slot)
    {
        return Persisted.TryGetValue(slot, out var value)
            ? RuntimeHotkeyService.NormalizeOrDefault(value, DefaultBindingOf(slot))
            : DefaultBindingOf(slot);
    }

    private static string DefaultBindingOf(string slot)
    {
        return slot == CancelSlot ? DefaultCancelBinding : $"Kp{slot}";
    }

    private static string IdFor(int index)
    {
        return $"{Entry.ModId}.minion_select.{index}";
    }

    /// <summary>
    /// 设置行写入回调：空值恢复默认；非法绑定拒绝（返回 false 时 UI 不更新）。
    /// 成功后重绑热键句柄并持久化。
    /// </summary>
    private static bool TryApplyBinding(string slot, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ApplyBinding(slot, DefaultBindingOf(slot));
            return true;
        }
        if (!RuntimeHotkeyService.TryNormalizeBinding(value, out var normalized))
        {
            return false;
        }
        ApplyBinding(slot, normalized);
        return true;
    }

    private static void ApplyBinding(string slot, string normalized)
    {
        Persisted[slot] = normalized;
        SavePersisted();
        if (int.TryParse(slot, out var index) && SelectHandles.TryGetValue(index, out var handle))
        {
            handle.TryRebind(normalized, out _);
        }
        else if (slot == CancelSlot && _cancelHandle != null)
        {
            _cancelHandle.TryRebind(normalized, out _);
        }
    }

    /// <summary>
    /// 在 RitsuLib 设置中心注册 Jaina 的「随从选中快捷键」页面（幂等）。
    /// 每行使用 RitsuLib 自带 AddKeyBinding 键位录制控件。
    /// </summary>
    private static void RegisterModSettingsPage()
    {
        if (_settingsPageRegistered)
        {
            return;
        }
        _settingsPageRegistered = true;

        ModSettingsRegistry.Register(
            Entry.ModId,
            page => page
                .WithTitle(ModSettingsText.LocString(
                    "gameplay_ui",
                    "JAINA_UI_MINION_SELECT_PAGE_TITLE",
                    "Minion Select Hotkeys"))
                .WithDescription(ModSettingsText.LocString(
                    "gameplay_ui",
                    "JAINA_UI_MINION_SELECT_PAGE_DESC",
                    "Select or cancel selection of your own minions. Click a row to record a new key; Clear resets to default; menu has Reset to default."))
                .AddSection("minion_select", section =>
                {
                    section
                        .WithTitle(ModSettingsText.LocString(
                            "gameplay_ui",
                            "JAINA_UI_MINION_SELECT_SECTION_TITLE",
                            "Minion selection keys"))
                        .WithDescription(ModSettingsText.LocString(
                            "gameplay_ui",
                            "JAINA_UI_MINION_SELECT_SECTION_DESC",
                            "Small-keypad 1-7 select the 1st-7th of your minions (gold frame + card preview); Esc cancels."));

                    for (var i = 1; i <= MinionCount; i++)
                    {
                        var slot = i.ToString();
                        section.AddKeyBinding(
                            $"minion_select_{i}",
                            ModSettingsText.LocString(
                                "gameplay_ui",
                                $"JAINA_UI_MINION_SELECT_{i}",
                                $"Select minion {i}"),
                            CreateBinding(slot),
                            allowModifierCombos: true,
                            allowModifierOnly: false,
                            description: ModSettingsText.LocString(
                                "gameplay_ui",
                                $"JAINA_UI_MINION_SELECT_{i}_DESC",
                                $"Toggle selection of your {i}th minion."));
                    }

                    section.AddKeyBinding(
                        "minion_select_cancel",
                        ModSettingsText.LocString(
                            "gameplay_ui",
                            "JAINA_UI_MINION_SELECT_CANCEL",
                            "Cancel selection"),
                        CreateBinding(CancelSlot),
                        allowModifierCombos: true,
                        allowModifierOnly: false,
                        description: ModSettingsText.LocString(
                            "gameplay_ui",
                            "JAINA_UI_MINION_SELECT_CANCEL_DESC",
                            "Deselect the currently selected minion."));
                }));
    }

    /// <summary>
    /// 创建设置行绑定：读当前绑定、写时应用+重绑+持久化、默认值工厂供 Reset to default。
    /// </summary>
    private static IModSettingsValueBinding<string> CreateBinding(string slot)
    {
        return ModSettingsBindings.WithDefault(
            ModSettingsBindings.Callback(
                Entry.ModId,
                $"minion_select_{slot}",
                () => GetBinding(slot),
                value => TryApplyBinding(slot, value),
                SavePersisted),
            () => DefaultBindingOf(slot));
    }

    private static string Loc(string key, string fallback)
    {
        return LocString.GetIfExists("gameplay_ui", key)?.GetFormattedText() ?? fallback;
    }

    private static void LoadPersisted()
    {
        try
        {
            if (!Godot.FileAccess.FileExists(StoragePath))
            {
                return;
            }
            using var file = Godot.FileAccess.Open(StoragePath, Godot.FileAccess.ModeFlags.Read);
            var text = file.GetAsText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
            if (data == null)
            {
                return;
            }
            foreach (var (key, value) in data)
            {
                Persisted[key] = value;
            }
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[JainaSelect] load persisted failed: {ex.Message}");
        }
    }

    private static void SavePersisted()
    {
        try
        {
            using var file = Godot.FileAccess.Open(StoragePath, Godot.FileAccess.ModeFlags.Write);
            file.StoreString(JsonSerializer.Serialize(Persisted));
        }
        catch (System.Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Warn($"[JainaSelect] save persisted failed: {ex.Message}");
        }
    }
}
