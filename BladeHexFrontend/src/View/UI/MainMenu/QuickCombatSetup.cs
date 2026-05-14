// QuickCombatSetup.cs
// 快速战斗配置面板 — 不依赖 UITheme/UIFactory，纯原生 Godot 控件
using Godot;
using BladeHex.Data;

namespace BladeHex.UI;

[GlobalClass]
public partial class QuickCombatSetup : CanvasLayer
{
    [Signal] public delegate void StartCombatEventHandler();
    [Signal] public delegate void BackPressedEventHandler();

    private Control _root = null!;
    private OptionButton _sizeOption = null!;
    private HSlider _playerCountSlider = null!;
    private Label _playerCountLabel = null!;
    private HSlider _enemyCountSlider = null!;
    private Label _enemyCountLabel = null!;
    private OptionButton _difficultyOption = null!;
    private OptionButton _weatherOption = null!;

    public override void _Ready()
    {
        Layer = 100; // 确保渲染在主菜单之上
        BuildUI();
        _root.Visible = false;
    }

    public void ShowPanel()
    {
        _root.Visible = true;
        _root.MouseFilter = Control.MouseFilterEnum.Stop;
    }

    public void HidePanel()
    {
        _root.Visible = false;
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private void BuildUI()
    {
        _root = new Control();
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(_root);

        // 半透明背景
        var overlay = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(overlay);

        // 居中面板
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(center);

        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(600, 450);
        var panelStyle = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f) };
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = new Color(0.5f, 0.45f, 0.3f, 0.8f);
        panelStyle.SetCornerRadiusAll(10);
        panelStyle.SetContentMarginAll(30);
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(vbox);

        // 标题
        var title = new Label { Text = "快速战斗配置" };
        title.AddThemeFontSizeOverride("font_size", 26);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.88f, 0.6f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        // 分隔线
        var sep = new HSeparator();
        vbox.AddChild(sep);

        // 配置网格
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 20);
        grid.AddThemeConstantOverride("v_separation", 14);
        vbox.AddChild(grid);

        // 规模
        AddLabel(grid, "战斗规模");
        _sizeOption = new OptionButton { CustomMinimumSize = new Vector2(250, 38) };
        _sizeOption.AddItem("小型 (15×10)", 0);
        _sizeOption.AddItem("中型 (18×12)", 1);
        _sizeOption.AddItem("大型 (24×16)", 2);
        _sizeOption.AddThemeFontSizeOverride("font_size", 15);
        grid.AddChild(_sizeOption);

        // 玩家数量
        AddLabel(grid, "玩家单位数");
        var phbox = new HBoxContainer();
        phbox.AddThemeConstantOverride("separation", 10);
        _playerCountSlider = new HSlider { MinValue = 1, MaxValue = 6, Value = 2, CustomMinimumSize = new Vector2(180, 0) };
        _playerCountLabel = new Label { Text = "2" };
        _playerCountLabel.AddThemeFontSizeOverride("font_size", 16);
        _playerCountSlider.ValueChanged += (v) => _playerCountLabel.Text = ((int)v).ToString();
        phbox.AddChild(_playerCountSlider);
        phbox.AddChild(_playerCountLabel);
        grid.AddChild(phbox);

        // 敌方数量
        AddLabel(grid, "敌方单位数");
        var ehbox = new HBoxContainer();
        ehbox.AddThemeConstantOverride("separation", 10);
        _enemyCountSlider = new HSlider { MinValue = 1, MaxValue = 10, Value = 3, CustomMinimumSize = new Vector2(180, 0) };
        _enemyCountLabel = new Label { Text = "3" };
        _enemyCountLabel.AddThemeFontSizeOverride("font_size", 16);
        _enemyCountSlider.ValueChanged += (v) => _enemyCountLabel.Text = ((int)v).ToString();
        ehbox.AddChild(_enemyCountSlider);
        ehbox.AddChild(_enemyCountLabel);
        grid.AddChild(ehbox);

        // 难度
        AddLabel(grid, "敌方难度");
        _difficultyOption = new OptionButton { CustomMinimumSize = new Vector2(250, 38) };
        _difficultyOption.AddItem("简单", 0);
        _difficultyOption.AddItem("普通", 1);
        _difficultyOption.AddItem("困难", 2);
        _difficultyOption.Selected = 1;
        _difficultyOption.AddThemeFontSizeOverride("font_size", 15);
        grid.AddChild(_difficultyOption);

        // 天气
        AddLabel(grid, "天气");
        _weatherOption = new OptionButton { CustomMinimumSize = new Vector2(250, 38) };
        _weatherOption.AddItem("晴天", 0);
        _weatherOption.AddItem("雨天", 1);
        _weatherOption.AddItem("雪天", 2);
        _weatherOption.AddItem("沙尘暴", 3);
        _weatherOption.Selected = 0;
        _weatherOption.AddThemeFontSizeOverride("font_size", 15);
        grid.AddChild(_weatherOption);

        // 分隔线
        vbox.AddChild(new HSeparator());

        // 按钮行
        var buttons = new HBoxContainer();
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        buttons.AddThemeConstantOverride("separation", 40);
        vbox.AddChild(buttons);

        var back = MakeButton("返回", 120);
        back.Pressed += () => { HidePanel(); EmitSignal(SignalName.BackPressed); };
        buttons.AddChild(back);

        var start = MakeButton("开始战斗", 160);
        start.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.4f));
        start.Pressed += OnStartPressed;
        buttons.AddChild(start);
    }

    private void OnStartPressed()
    {
        var gs = GetNode<GlobalState>("/root/GlobalState");
        gs.QuickCombatSize = _sizeOption.Selected;
        gs.QuickCombatPlayerCount = (int)_playerCountSlider.Value;
        gs.QuickCombatEnemyCount = (int)_enemyCountSlider.Value;
        gs.QuickCombatDifficulty = _difficultyOption.Selected;

        // 天气：0=晴, 1=雨, 2=雪, 3=沙暴 → 映射到 WeatherType (-1, 0, 1, 2)
        gs.CurrentWeatherType = _weatherOption.Selected - 1;
        gs.CurrentWeatherIntensity = _weatherOption.Selected > 0 ? 0.7f : 0.0f;

        HidePanel();
        EmitSignal(SignalName.StartCombat);
    }

    private static void AddLabel(Control parent, string text)
    {
        var lbl = new Label { Text = text, CustomMinimumSize = new Vector2(130, 0) };
        lbl.AddThemeFontSizeOverride("font_size", 16);
        lbl.AddThemeColorOverride("font_color", new Color(0.85f, 0.82f, 0.75f));
        parent.AddChild(lbl);
    }

    private static Button MakeButton(string text, int width)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(width, 44) };
        btn.AddThemeFontSizeOverride("font_size", 16);

        var normal = new StyleBoxFlat { BgColor = new Color(0.15f, 0.14f, 0.18f) };
        normal.SetBorderWidthAll(1);
        normal.BorderColor = new Color(0.35f, 0.32f, 0.28f);
        normal.SetCornerRadiusAll(6);
        normal.SetContentMarginAll(8);
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat { BgColor = new Color(0.25f, 0.22f, 0.28f) };
        hover.SetBorderWidthAll(1);
        hover.BorderColor = new Color(0.55f, 0.48f, 0.3f);
        hover.SetCornerRadiusAll(6);
        hover.SetContentMarginAll(8);
        btn.AddThemeStyleboxOverride("hover", hover);

        return btn;
    }
}
