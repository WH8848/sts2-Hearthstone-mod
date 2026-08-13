using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace jaina.Scripts.Character.Minions;

/// <summary>
/// 随从悬停卡牌面板：鼠标悬停场上随从时，在其左侧或右侧悬浮显示
/// 该随从的卡牌信息（卡图原画 + 名称 + 属性 + 效果描述）。
/// </summary>
public partial class JainaMinionTooltip : Control
{
    /// <summary>
    /// 面板宽度（含卡图与文本区）
    /// </summary>
    private const float PanelWidth = 268f;

    private Panel _panel;
    private TextureRect _portrait;
    private Label _nameLabel;
    private Label _statsLabel;
    private RichTextLabel _descLabel;

    /// <summary>
    /// 实时属性文本的来源（悬停时动态刷新攻击/生命）
    /// </summary>
    private JainaMinionBase? _minion;

    /// <summary>
    /// 卡名（本地化后文本）
    /// </summary>
    private string _title = "";

    /// <summary>
    /// 关键词行（冲锋/亡语等）
    /// </summary>
    private string _keywordsLine = "";

    /// <summary>
    /// 效果描述（本地化后文本）
    /// </summary>
    private string _description = "";

    public JainaMinionTooltip()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 100;

        _panel = new Panel
        {
            Size = new Vector2(PanelWidth, 0f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.09f, 0.08f, 0.1f, 0.96f),
            BorderColor = new Color(0.75f, 0.65f, 0.35f, 0.9f),
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginBottom = 8f,
            ContentMarginTop = 8f,
            ContentMarginLeft = 8f,
            ContentMarginRight = 8f
        };
        style.SetBorderWidthAll(2);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        var box = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
        };
        _panel.AddChild(box);
        box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 8);

        _portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(252f, 192f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        box.AddChild(_portrait);

        _nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(_nameLabel);

        _statsLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _statsLabel.AddThemeFontSizeOverride("font_size", 16);
        box.AddChild(_statsLabel);

        _descLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(252f, 0f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _descLabel.AddThemeFontSizeOverride("normal_font_size", 13);
        box.AddChild(_descLabel);
    }

    /// <summary>
    /// 初始化面板内容（卡图/卡名/关键词/描述 + 实时属性来源）
    /// </summary>
    public void Setup(JainaMinionBase minion, Texture2D portrait, string title, string keywordsLine, string description)
    {
        _minion = minion;
        _portrait.Texture = portrait;
        _title = title;
        _keywordsLine = keywordsLine;
        _description = description;
        _nameLabel.Text = title;
        _descLabel.Text = description;
    }

    /// <summary>
    /// 在指定一侧显示（showOnLeft=true 时面板出现在随从左侧，否则右侧）
    /// </summary>
    public void ShowTip(bool showOnLeft)
    {
        if (_minion == null)
        {
            return;
        }
        // 实时刷新属性（攻击/生命会随战斗变化）
        _statsLabel.Text = $"[color=#ffd75e]{_minion.BaseAttackValue}[/color] / [color=#ff6b5e]{_minion.Creature.CurrentHp}[/color]"
            + (string.IsNullOrEmpty(_keywordsLine) ? "" : $"  {_keywordsLine}");

        var panelHeight = 192f + 36f + 24f + Mathf.Max(40f, _descLabel.GetMinimumSize().Y + 16f);
        _panel.Size = new Vector2(PanelWidth, panelHeight);
        Position = showOnLeft
            ? new Vector2(-PanelWidth - 90f, -panelHeight / 2f)
            : new Vector2(90f, -panelHeight / 2f);
        Visible = true;
        ZIndex = 100;
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public void HideTip()
    {
        Visible = false;
    }
}
