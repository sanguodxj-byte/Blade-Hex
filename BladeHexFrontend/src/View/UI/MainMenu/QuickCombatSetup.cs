// QuickCombatSetup.cs
// 快速战斗配置面板 — 不依赖 UITheme/UIFactory，纯原生 Godot 控件
using Godot;
using BladeHex.Data;
using BladeHex.Localization;

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
    private HSlider _levelSlider = null!;
    private Label _levelLabel = null!;
    private OptionButton _enemyTypeOption = null!;
    private OptionButton _legendaryTypeOption = null!;
    private Label _legendaryTypeLabel = null!;
    private OptionButton _templateOption = null!;

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
        var title = new Label { Text = L10n.Tr("QC_TITLE") };
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
        AddLabel(grid, L10n.Tr("QC_BATTLE_SIZE"));
        _sizeOption = new OptionButton { CustomMinimumSize = new Vector2(250, 38) };
        _sizeOption.AddItem(L10n.Tr("QC_SIZE_SMALL"), 0);
        _sizeOption.AddItem(L10n.Tr("QC_SIZE_MEDIUM"), 1);
        _sizeOption.AddItem(L10n.Tr("QC_SIZE_LARGE"), 2);
        _sizeOption.AddItem(L10n.Tr("QC_SIZE_HUGE"), 3);
        _sizeOption.AddThemeFontSizeOverride("font_size", 15);
        grid.AddChild(_sizeOption);

        // 玩家数量
        AddLabel(grid, L10n.Tr("QC_PLAYER_UNITS"));
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
        AddLabel(grid, L10n.Tr("QC_ENEMY_UNITS"));
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
        AddLabel(grid, L10n.Tr("QC_ENEMY_DIFFICULTY"));
        _difficultyOption = new OptionButton { CustomMinimumSize = new Vector2(250, 38) };
        _difficultyOption.AddItem(L10n.Tr("DIFFICULTY_EASY"), 0);
        _difficultyOption.AddItem(L10n.Tr("DIFFICULTY_NORMAL"), 1);
        _difficultyOption.AddItem(L10n.Tr("DIFFICULTY_HARD"), 2);
        _difficultyOption.Selected = 1;
        _difficultyOption.AddThemeFontSizeOverride("font_size", 15);
        grid.AddChild(_difficultyOption);

        // 等级
        AddLabel(grid, L10n.Tr("QC_CHARACTER_LEVEL"));
        var lvlHbox = new HBoxContainer();
        lvlHbox.AddThemeConstantOverride("separation", 10);
        _levelSlider = new HSlider { MinValue = 1, MaxValue = 120, Value = 1, Step = 1, CustomMinimumSize = new Vector2(180, 0) };
        _levelLabel = new Label { Text = "1" };
        _levelLabel.AddThemeFontSizeOverride("font_size", 16);
        _levelSlider.ValueChanged += (v) => _levelLabel.Text = ((int)v).ToString();
        lvlHbox.AddChild(_levelSlider);
        lvlHbox.AddChild(_levelLabel);
        grid.AddChild(lvlHbox);

        // 敌方种类
        AddLabel(grid, L10n.Tr("QC_ENEMY_TYPE"));
        _enemyTypeOption = new OptionButton { CustomMinimumSize = new Vector2(250, 38) };
        _enemyTypeOption.AddItem(L10n.Tr("ENEMY_HUMANOID"), 0);
        _enemyTypeOption.AddItem(L10n.Tr("ENEMY_UNDEAD"), 1);
        _enemyTypeOption.AddItem(L10n.Tr("ENEMY_BEAST"), 2);
        _enemyTypeOption.AddItem(L10n.Tr("ENEMY_MIXED"), 3);
        _enemyTypeOption.AddItem(L10n.Tr("ENEMY_LEGENDARY"), 4);
        _enemyTypeOption.Selected = 0;
        _enemyTypeOption.AddThemeFontSizeOverride("font_size", 15);
        _enemyTypeOption.ItemSelected += OnEnemyTypeChanged;
        grid.AddChild(_enemyTypeOption);

        // 传奇生物类型（默认隐藏，选择"传奇生物"后显示）
        _legendaryTypeLabel = new Label { Text = L10n.Tr("QC_LEGENDARY_TYPE"), CustomMinimumSize = new Vector2(130, 0), Visible = false };
        _legendaryTypeLabel.AddThemeFontSizeOverride("font_size", 16);
        _legendaryTypeLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.82f, 0.75f));
        grid.AddChild(_legendaryTypeLabel);

        _legendaryTypeOption = new OptionButton { CustomMinimumSize = new Vector2(250, 38), Visible = false };
        _legendaryTypeOption.AddItem(L10n.Tr("OPTION_RANDOM"), 0);
        // 龙族
        _legendaryTypeOption.AddItem("少年红龙 (Lv.78)", 1);
        _legendaryTypeOption.AddItem("成年红龙 (Lv.90)", 2);
        _legendaryTypeOption.AddItem("远古冰龙 (Lv.96)", 3);
        _legendaryTypeOption.AddItem("陨星之龙 (Lv.120)", 4);
        _legendaryTypeOption.AddItem("世界之蛇 (Lv.120)", 5);
        // 亡灵
        _legendaryTypeOption.AddItem("远古巫妖 (Lv.96)", 6);
        _legendaryTypeOption.AddItem("死亡骑士王 (Lv.102)", 7);
        _legendaryTypeOption.AddItem("食尸鬼之王 (Lv.84)", 8);
        _legendaryTypeOption.AddItem("木乃伊君主 (Lv.96)", 9);
        _legendaryTypeOption.AddItem("幽影母亲 (Lv.90)", 10);
        _legendaryTypeOption.AddItem("死亡圣者 (Lv.96)", 11);
        _legendaryTypeOption.AddItem("女妖之女王 (Lv.90)", 12);
        // 魔物
        _legendaryTypeOption.AddItem("熔岩领主 (Lv.108)", 13);
        _legendaryTypeOption.AddItem("深渊领主 (Lv.108)", 14);
        _legendaryTypeOption.AddItem("魔眼暴君 (Lv.96)", 15);
        _legendaryTypeOption.AddItem("美杜莎 (Lv.78)", 16);
        _legendaryTypeOption.AddItem("夺心魔皇 (Lv.102)", 17);
        _legendaryTypeOption.AddItem("深坑魔王 (Lv.108)", 18);
        _legendaryTypeOption.AddItem("刻耳柏洛斯 (Lv.90)", 19);
        _legendaryTypeOption.AddItem("炎魔 (Lv.108)", 20);
        // 野兽
        _legendaryTypeOption.AddItem("九头蛇 (Lv.90)", 21);
        _legendaryTypeOption.AddItem("奇美拉 (Lv.84)", 22);
        _legendaryTypeOption.AddItem("克拉肯 (Lv.108)", 23);
        _legendaryTypeOption.AddItem("狮鹫之王 (Lv.84)", 24);
        _legendaryTypeOption.AddItem("紫色蠕虫 (Lv.90)", 25);
        // 巨型
        _legendaryTypeOption.AddItem("风暴巨人 (Lv.102)", 26);
        _legendaryTypeOption.AddItem("雪人首领 (Lv.84)", 27);
        // 构造体
        _legendaryTypeOption.AddItem("觉醒远古机兵 (Lv.108)", 28);
        _legendaryTypeOption.AddItem("古树长老 (Lv.96)", 29);
        _legendaryTypeOption.AddItem("不朽巨像 (Lv.114)", 30);
        // 人形
        _legendaryTypeOption.AddItem("大法师 (Lv.96)", 31);
        _legendaryTypeOption.Selected = 0;
        _legendaryTypeOption.AddThemeFontSizeOverride("font_size", 15);
        grid.AddChild(_legendaryTypeOption);

        // 地形模板
        AddLabel(grid, L10n.Tr("QC_BATTLE_TERRAIN"));
        _templateOption = new OptionButton { CustomMinimumSize = new Vector2(250, 38) };
        _templateOption.AddItem(L10n.Tr("OPTION_RANDOM"), 0);
        _templateOption.AddItem(L10n.Tr("TEMPLATE_PLAIN_FIELD"), 1);
        _templateOption.AddItem(L10n.Tr("TEMPLATE_FOREST_AMBUSH"), 2);
        _templateOption.AddItem(L10n.Tr("TEMPLATE_MOUNTAIN_PASS"), 3);
        _templateOption.AddItem(L10n.Tr("TEMPLATE_SWAMP_BATTLE"), 4);
        _templateOption.AddItem(L10n.Tr("TEMPLATE_COASTAL_AMBUSH"), 5);
        _templateOption.AddItem(L10n.Tr("TEMPLATE_DESERT_SKIRMISH"), 6);
        _templateOption.AddItem(L10n.Tr("TEMPLATE_VILLAGE_DEFENSE"), 7);
        _templateOption.AddItem(L10n.Tr("TEMPLATE_RUINS_EXPLORATION"), 8);
        _templateOption.AddItem(L10n.Tr("TEMPLATE_CASTLE_SIEGE"), 9);
        _templateOption.AddItem(L10n.Tr("TEMPLATE_CASTLE_DEFENSE"), 10);
        _templateOption.Selected = 0;
        _templateOption.ItemSelected += OnTemplateChanged;
        _templateOption.AddThemeFontSizeOverride("font_size", 15);
        grid.AddChild(_templateOption);

        // 天气
        AddLabel(grid, L10n.Tr("QC_WEATHER"));
        _weatherOption = new OptionButton { CustomMinimumSize = new Vector2(250, 38) };
        _weatherOption.AddItem(L10n.Tr("WEATHER_CLEAR"), 0);
        _weatherOption.AddItem(L10n.Tr("WEATHER_RAIN"), 1);
        _weatherOption.AddItem(L10n.Tr("WEATHER_SNOW"), 2);
        _weatherOption.AddItem(L10n.Tr("WEATHER_SANDSTORM"), 3);
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

        var back = MakeButton(L10n.Tr("MENU_BACK"), 120);
        back.Pressed += () => { HidePanel(); EmitSignal(SignalName.BackPressed); };
        buttons.AddChild(back);

        var start = MakeButton(L10n.Tr("QC_START_COMBAT"), 160);
        start.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.4f));
        start.Pressed += OnStartPressed;
        buttons.AddChild(start);
    }

    private static readonly string[] TemplateKeys =
    {
        "",                    // 0: 随机
        "plain_field",         // 1: 平原旷野
        "forest_ambush",       // 2: 森林伏击
        "mountain_pass",       // 3: 山间隘口
        "swamp_battle",        // 4: 沼泽遭遇
        "coastal_ambush",      // 5: 海岸伏击
        "desert_skirmish",     // 6: 沙漠冲突
        "village_defense",     // 7: 村庄防御
        "ruins_exploration",   // 8: 遗迹探索
        "castle_siege",        // 9: 攻城战（玩家攻城）
        "castle_defense",      // 10: 守城战（玩家守城）
    };

    private void OnEnemyTypeChanged(long idx)
    {
        bool isLegendary = idx == 4;
        _legendaryTypeLabel.Visible = isLegendary;
        _legendaryTypeOption.Visible = isLegendary;
    }

    private void OnTemplateChanged(long idx)
    {
        // 攻城/守城战强制锁定巨大规模
        bool isSiege = idx == 9 || idx == 10;
        if (isSiege)
        {
            _sizeOption.Selected = 3; // 巨大
            _sizeOption.Disabled = true;
        }
        else
        {
            _sizeOption.Disabled = false;
        }
    }

    private void OnStartPressed()
    {
        var gs = BladeHex.Data.Globals.State;
        gs.QuickCombat.Size = _sizeOption.Selected;
        gs.QuickCombat.PlayerCount = (int)_playerCountSlider.Value;
        gs.QuickCombat.EnemyCount = (int)_enemyCountSlider.Value;
        gs.QuickCombat.Difficulty = _difficultyOption.Selected;
        gs.QuickCombat.PlayerLevel = (int)_levelSlider.Value;
        gs.QuickCombat.EnemyType = _enemyTypeOption.Selected;
        gs.QuickCombat.LegendaryType = _enemyTypeOption.Selected == 4
            ? _legendaryTypeOption.Selected - 1  // 0="随机" → -1, 1~N → 0~N-1
            : -1;

        // 地形模板
        int tplIdx = _templateOption.Selected;
        gs.QuickCombat.Template = tplIdx < TemplateKeys.Length ? TemplateKeys[tplIdx] : "";

        // 天气 — 通过 Autoload 直接设置
        var weatherMgr = BladeHex.Data.Globals.WeatherOrNull;
        if (weatherMgr != null)
        {
            int weatherIdx = _weatherOption.Selected - 1;
            var weatherType = weatherIdx < 0
                ? BladeHex.View.Environment.WeatherType.Clear
                : (BladeHex.View.Environment.WeatherType)weatherIdx;
            weatherMgr.SetWeatherImmediate(weatherType, BladeHex.View.Environment.WeatherIntensity.Moderate);
        }

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
