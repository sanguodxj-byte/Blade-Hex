// GameMenuManager.cs
// 全局 ESC 系统菜单 + 设置面板 — Autoload CanvasLayer
// 所有场景共享同一个实例，避免重复实现。
using BladeHex.Localization;
using Godot;

namespace BladeHex.UI.Global;

/// <summary>
/// [Autoload Singleton] 全局系统菜单管理器。
///
/// <para>注册位置：<c>project.godot [autoload]</c> 段，名称 <c>GameMenuManager</c>。</para>
/// <para>生命周期：应用全局。</para>
/// <para>访问方式：建议通过 <see cref="BladeHex.Data.Globals.GameMenu"/> 或 <see cref="BladeHex.Data.Globals.GameMenuOrNull"/>。</para>
/// <para>职责：ESC 菜单（返回游戏 / 设置 / 保存 / 加载 / 回主菜单 / 退出）和完整设置面板。</para>
/// </summary>
[GlobalClass]
public partial class GameMenuManager : CanvasLayer
{
    [Signal] public delegate void ResumeRequestedEventHandler();
    [Signal] public delegate void SaveRequestedEventHandler();
    [Signal] public delegate void LoadRequestedEventHandler();
    [Signal] public delegate void ReturnToMainMenuRequestedEventHandler();

    private Control _root = null!;
    private Control _menuPanel = null!;
    private Control _settingsPanel = null!;

    /// <summary>当前是否在主菜单场景（主菜单不显示"返回游戏"和"保存"按钮）</summary>
    public bool IsInMainMenu { get; set; } = false;

    /// <summary>是否打开</summary>
    public bool IsOpen => Visible;

    // 主题色
    private static readonly Color BgPrimary = new(0.10f, 0.09f, 0.07f, 0.96f);
    private static readonly Color BgOverlay = new(0, 0, 0, 0.55f);
    private static readonly Color BorderDefault = new(0.45f, 0.38f, 0.28f, 1.0f);
    private static readonly Color BorderHighlight = new(0.85f, 0.72f, 0.45f, 1.0f);
    private static readonly Color TextAccent = new(0.95f, 0.85f, 0.55f, 1.0f);
    private static readonly Color TextNegative = new(0.92f, 0.45f, 0.40f, 1.0f);
    private const int FontMd = 16;
    private const int FontXl = 22;
    private const int Spacing = 12;
    private const int Radius = 8;
    private const int BtnHeight = 44;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 200; // 最高层

        _root = new Control { Name = "MenuRoot" };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.MouseFilter = Control.MouseFilterEnum.Stop;
        _root.ProcessMode = ProcessModeEnum.Always;
        AddChild(_root);

        BuildMenuPanel();
        BuildSettingsPanel();

        LanguageManager.Instance.LocaleChanged += _ => RebuildUiForLocale();

        Visible = false;
    }

    // ============================================================
    // 公共 API
    // ============================================================

    public void Toggle()
    {
        if (Visible) Close();
        else Open();
    }

    public void Open()
    {
        Visible = true;
        GetTree().Paused = true;
        _settingsPanel.Visible = false;
        _menuPanel.Visible = true;
    }

    public void Close()
    {
        Visible = false;
        GetTree().Paused = false;
        _settingsPanel.Visible = false;
        EmitSignal(SignalName.ResumeRequested);
    }

    public void OpenSettings()
    {
        Visible = true;
        GetTree().Paused = true;
        _menuPanel.Visible = false;
        _settingsPanel.Visible = true;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;

        if (@event is InputEventKey k)
        {
            // ESC 关闭设置面板或关闭整个菜单
            if (k.Pressed && !k.Echo && k.Keycode == Key.Escape)
            {
                if (_settingsPanel.Visible)
                {
                    _settingsPanel.Visible = false;
                    _menuPanel.Visible = true;
                }
                else
                {
                    Close();
                }
            }
            // 阻止所有键盘事件穿透到暂停的场景
            GetViewport().SetInputAsHandled();
        }
    }

    // ============================================================
    // ESC 菜单面板
    // ============================================================

    private void RebuildUiForLocale()
    {
        bool wasOpen = Visible;
        bool wasSettingsOpen = _settingsPanel?.Visible == true;

        _menuPanel?.QueueFree();
        _settingsPanel?.QueueFree();
        BuildMenuPanel();
        BuildSettingsPanel();

        Visible = wasOpen;
        if (_menuPanel != null)
            _menuPanel.Visible = wasOpen && !wasSettingsOpen;
        if (_settingsPanel != null)
            _settingsPanel.Visible = wasOpen && wasSettingsOpen;
    }

    private void BuildMenuPanel()
    {
        _menuPanel = new PanelContainer();
        var overlayBg = new StyleBoxFlat { BgColor = BgOverlay };
        ((PanelContainer)_menuPanel).AddThemeStyleboxOverride("panel", overlayBg);
        _menuPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _menuPanel.MouseFilter = Control.MouseFilterEnum.Stop;
        _root.AddChild(_menuPanel);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _menuPanel.AddChild(center);

        var inner = new PanelContainer();
        inner.AddThemeStyleboxOverride("panel", MakeStyle(BgPrimary, BorderHighlight, 2, Radius, 30));
        center.AddChild(inner);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", Spacing);
        vbox.CustomMinimumSize = new Vector2(240, 0);
        inner.AddChild(vbox);

        var title = new Label { Text = L10n.Tr("MENU_SYSTEM_TITLE"), HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", FontXl);
        title.AddThemeColorOverride("font_color", TextAccent);
        vbox.AddChild(title);

        var resumeBtn = MakeBtn(L10n.Tr("MENU_RESUME_GAME"));
        resumeBtn.Pressed += Close;
        vbox.AddChild(resumeBtn);

        var saveBtn = MakeBtn(L10n.Tr("MENU_SAVE_GAME"));
        saveBtn.Pressed += () => { EmitSignal(SignalName.SaveRequested); Close(); };
        vbox.AddChild(saveBtn);

        var loadBtn = MakeBtn(L10n.Tr("MENU_LOAD_GAME"));
        loadBtn.Pressed += () => { EmitSignal(SignalName.LoadRequested); Close(); };
        vbox.AddChild(loadBtn);

        var settingsBtn = MakeBtn(L10n.Tr("MENU_SETTINGS"));
        settingsBtn.Pressed += OpenSettings;
        vbox.AddChild(settingsBtn);

        var mainMenuBtn = MakeBtn(L10n.Tr("MENU_MAIN_MENU"));
        mainMenuBtn.Pressed += () =>
        {
            GetTree().Paused = false;
            Visible = false;
            EmitSignal(SignalName.ReturnToMainMenuRequested);
            BladeHex.View.SceneTransition.ChangeSceneTo(GetTree(), "res://BladeHexFrontend/src/ui/main_menu/main_menu.tscn");
        };
        vbox.AddChild(mainMenuBtn);

        var exitBtn = MakeBtn(L10n.Tr("MENU_QUIT_GAME"));
        exitBtn.AddThemeColorOverride("font_color", TextNegative);
        exitBtn.Pressed += () => { GetTree().Paused = false; GetTree().Quit(); };
        vbox.AddChild(exitBtn);
    }

    // ============================================================
    // 设置面板（选项卡式：游戏 / 音频 / 画面 / 控制）
    // ============================================================

    private void BuildSettingsPanel()
    {
        _settingsPanel = new PanelContainer();
        _settingsPanel.ProcessMode = ProcessModeEnum.Always;
        _settingsPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _settingsPanel.MouseFilter = Control.MouseFilterEnum.Stop;
        var bg = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.75f) };
        ((PanelContainer)_settingsPanel).AddThemeStyleboxOverride("panel", bg);
        _settingsPanel.Visible = false;
        _root.AddChild(_settingsPanel);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _settingsPanel.AddChild(center);

        // 外框
        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 12);
        center.AddChild(outer);

        // 标题
        var title = new Label { Text = L10n.Tr("SETTINGS_TITLE"), HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", TextAccent);
        outer.AddChild(title);

        // TabContainer
        var tabs = new TabContainer();
        tabs.CustomMinimumSize = new Vector2(580, 500);
        tabs.AddThemeFontSizeOverride("font_size", FontMd);
        var tabBg = MakeStyle(BgPrimary, BorderHighlight, 2, Radius, 16);
        tabs.AddThemeStyleboxOverride("panel", tabBg);
        outer.AddChild(tabs);

        // --- Tab 1: 游戏 ---
        var gameScroll = new ScrollContainer { Name = L10n.Tr("SETTINGS_TAB_GAME"), HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        tabs.AddChild(gameScroll);
        var gv = new VBoxContainer(); gv.AddThemeConstantOverride("separation", 10); gv.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        gameScroll.AddChild(gv);
        AddLocaleSelector(gv);
        AddSlider(gv, L10n.Tr("SETTINGS_GAME_SPEED"), 1, 5, 2, v => SetGlobal("game_speed_multiplier", (float)v));
        AddSlider(gv, L10n.Tr("SETTINGS_AUTOSAVE_INTERVAL"), 1, 30, 5, v => SetGlobal("autosave_interval_min", (int)v));
        AddSlider(gv, L10n.Tr("SETTINGS_COMBAT_ANIM_SPEED"), 1, 3, 1, v => SetGlobal("combat_anim_speed", (float)v));
        gv.AddChild(MakeSeparator());
        var dmgNum = MakeCheck(L10n.Tr("SETTINGS_SHOW_DAMAGE_NUMBERS"), true); dmgNum.Toggled += p => SetGlobal("show_damage_numbers", p); gv.AddChild(dmgNum);
        var tutorial = MakeCheck(L10n.Tr("SETTINGS_SHOW_TUTORIALS"), BladeHex.UI.Tutorial.TutorialManager.Instance?.IsEnabled ?? true);
        tutorial.Toggled += p =>
        {
            SetGlobal("show_tutorials", p);
            BladeHex.UI.Tutorial.TutorialManager.Instance?.SetEnabled(p);
        };
        gv.AddChild(tutorial);
        var autoEnd = MakeCheck(L10n.Tr("SETTINGS_AUTO_END_TURN"), false); autoEnd.Toggled += p => SetGlobal("auto_end_turn", p); gv.AddChild(autoEnd);
        var confirmRetreat = MakeCheck(L10n.Tr("SETTINGS_CONFIRM_RETREAT"), true); confirmRetreat.Toggled += p => SetGlobal("confirm_retreat", p); gv.AddChild(confirmRetreat);

        // --- Tab 2: 音频 ---
        var audioScroll = new ScrollContainer { Name = L10n.Tr("SETTINGS_TAB_AUDIO"), HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        tabs.AddChild(audioScroll);
        var av = new VBoxContainer(); av.AddThemeConstantOverride("separation", 10); av.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        audioScroll.AddChild(av);
        AddSlider(av, L10n.Tr("SETTINGS_MASTER_VOLUME"), 0, 100, 50, v => SetBusVolume("Master", v));
        AddSlider(av, L10n.Tr("SETTINGS_MUSIC_VOLUME"), 0, 100, 50, v => SetBusVolume("Music", v));
        AddSlider(av, L10n.Tr("SETTINGS_SFX_VOLUME"), 0, 100, 50, v => SetBusVolume("SFX", v));
        AddSlider(av, L10n.Tr("SETTINGS_AMBIENT_VOLUME"), 0, 100, 50, v => SetBusVolume("Ambient", v));
        av.AddChild(MakeSeparator());
        var mute = MakeCheck(L10n.Tr("SETTINGS_MUTE_ON_FOCUS_LOSS"), true); mute.Toggled += p => SetGlobal("mute_on_focus_loss", p); av.AddChild(mute);

        // --- Tab 3: 画面 ---
        var displayScroll = new ScrollContainer { Name = L10n.Tr("SETTINGS_TAB_DISPLAY"), HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        tabs.AddChild(displayScroll);
        var dv = new VBoxContainer(); dv.AddThemeConstantOverride("separation", 10); dv.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        displayScroll.AddChild(dv);
        var fullscreen = MakeCheck(L10n.Tr("SETTINGS_FULLSCREEN"), DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen);
        fullscreen.Toggled += p => DisplayServer.WindowSetMode(p ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed); dv.AddChild(fullscreen);
        var vsync = MakeCheck(L10n.Tr("SETTINGS_VSYNC"), DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled);
        vsync.Toggled += p => DisplayServer.WindowSetVsyncMode(p ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled); dv.AddChild(vsync);
        var shake = MakeCheck(L10n.Tr("SETTINGS_CAMERA_SHAKE"), true); shake.Toggled += p => SetGlobal("camera_shake_enabled", p); dv.AddChild(shake);
        var particles = MakeCheck(L10n.Tr("SETTINGS_WEATHER_PARTICLES"), true); particles.Toggled += p => SetGlobal("weather_particles_enabled", p); dv.AddChild(particles);
        dv.AddChild(MakeSeparator());
        dv.AddChild(MakeLabel(L10n.Tr("SETTINGS_RESOLUTION")));
        var resOpt = new OptionButton { CustomMinimumSize = new Vector2(250, 38) };
        resOpt.AddThemeFontSizeOverride("font_size", 15);
        resOpt.AddItem("1280 × 720", 0); resOpt.AddItem("1600 × 900", 1); resOpt.AddItem("1920 × 1080", 2); resOpt.AddItem("2560 × 1440", 3);
        var cur = DisplayServer.WindowGetSize();
        resOpt.Selected = cur.X >= 2560 ? 3 : cur.X >= 1920 ? 2 : cur.X >= 1600 ? 1 : 0;
        resOpt.ItemSelected += idx => { Vector2I sz = idx switch { 1 => new(1600, 900), 2 => new(1920, 1080), 3 => new(2560, 1440), _ => new(1280, 720) }; DisplayServer.WindowSetSize(sz); DisplayServer.WindowSetPosition((DisplayServer.ScreenGetSize() - sz) / 2); };
        dv.AddChild(resOpt);

        // --- Tab 4: 控制 ---
        var controlScroll = new ScrollContainer { Name = L10n.Tr("SETTINGS_TAB_CONTROLS"), HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever };
        tabs.AddChild(controlScroll);
        var cv = new VBoxContainer(); cv.AddThemeConstantOverride("separation", 10); cv.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        controlScroll.AddChild(cv);
        cv.AddChild(MakeLabel(L10n.Tr("SETTINGS_CONTROL_CAMERA_PAN")));
        cv.AddChild(MakeLabel(L10n.Tr("SETTINGS_CONTROL_ZOOM")));
        cv.AddChild(MakeLabel(L10n.Tr("SETTINGS_CONTROL_END_TURN")));
        cv.AddChild(MakeLabel(L10n.Tr("SETTINGS_CONTROL_SWITCH_UNIT")));
        cv.AddChild(MakeLabel(L10n.Tr("SETTINGS_CONTROL_CANCEL")));
        cv.AddChild(MakeLabel(L10n.Tr("SETTINGS_CONTROL_SYSTEM_MENU")));
        cv.AddChild(MakeSeparator());
        var edgePan = MakeCheck(L10n.Tr("SETTINGS_EDGE_PAN"), false); edgePan.Toggled += p => SetGlobal("edge_pan_enabled", p); cv.AddChild(edgePan);
        AddSlider(cv, L10n.Tr("SETTINGS_CAMERA_PAN_SPEED"), 1, 10, 5, v => SetGlobal("camera_pan_speed", (float)v));

        // --- 返回按钮 ---
        var closeBtn = MakeBtn(L10n.Tr("MENU_BACK"));
        closeBtn.CustomMinimumSize = new Vector2(180, 48);
        closeBtn.Pressed += () =>
        {
            _settingsPanel.Visible = false;
            if (IsInMainMenu)
            {
                // 主菜单：关闭整个 GameMenu，返回主菜单
                Close();
            }
            else
            {
                // 游戏中：返回 ESC 菜单
                _menuPanel.Visible = true;
            }
        };
        outer.AddChild(closeBtn);
    }

    // ============================================================
    // 工具方法
    // ============================================================

    private static void SetBusVolume(string busName, double val)
    {
        int idx = AudioServer.GetBusIndex(busName);
        if (idx < 0) idx = 0;
        float db = val > 0 ? Mathf.LinearToDb((float)val / 100.0f) : -80.0f;
        AudioServer.SetBusVolumeDb(idx, db);
    }

    private static void AddLocaleSelector(VBoxContainer vbox)
    {
        vbox.AddChild(MakeLabel(L10n.Tr("SETTINGS_LANGUAGE")));
        var localeOption = new OptionButton { CustomMinimumSize = new Vector2(250, 38) };
        localeOption.AddThemeFontSizeOverride("font_size", FontMd);

        for (int i = 0; i < LanguageManager.SupportedLocales.Length; i++)
        {
            string locale = LanguageManager.SupportedLocales[i];
            localeOption.AddItem(LanguageManager.LocaleNames.TryGetValue(locale, out string? displayName) ? displayName : locale, i);
            if (locale == LanguageManager.Instance.GetLocale())
                localeOption.Selected = i;
        }

        localeOption.ItemSelected += index =>
        {
            int i = (int)index;
            if (i >= 0 && i < LanguageManager.SupportedLocales.Length)
                LanguageManager.Instance.SetLocale(LanguageManager.SupportedLocales[i]);
        };
        vbox.AddChild(localeOption);
    }

    private void SetGlobal(string key, Variant value)
    {
        var gs = BladeHex.Data.Globals.StateOrNull;
        gs?.Set(key, value);
    }

    private Button MakeBtn(string text)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(200, BtnHeight) };
        btn.AddThemeFontSizeOverride("font_size", FontMd);
        var normal = new StyleBoxFlat { BgColor = new Color(0.16f, 0.14f, 0.11f) };
        normal.SetBorderWidthAll(2); normal.BorderColor = BorderDefault; normal.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("normal", normal);
        var hover = new StyleBoxFlat { BgColor = new Color(0.22f, 0.18f, 0.14f) };
        hover.SetBorderWidthAll(2); hover.BorderColor = BorderHighlight; hover.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        return btn;
    }

    private static StyleBoxFlat MakeStyle(Color bg, Color border, int borderWidth, int radius, int margin)
    {
        var s = new StyleBoxFlat { BgColor = bg };
        s.SetBorderWidthAll(borderWidth); s.BorderColor = border;
        s.SetCornerRadiusAll(radius); s.SetContentMarginAll(margin);
        return s;
    }

    private static HSeparator MakeSeparator() => new() { Modulate = new Color(1, 1, 1, 0.3f) };

    private static void AddSection(VBoxContainer vbox, string title)
    {
        var l = new Label { Text = title };
        l.AddThemeFontSizeOverride("font_size", 20);
        l.AddThemeColorOverride("font_color", TextAccent);
        vbox.AddChild(l);
    }

    private static Label MakeLabel(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", 15);
        return l;
    }

    private static CheckBox MakeCheck(string text, bool initial)
    {
        var c = new CheckBox { Text = text, ButtonPressed = initial };
        c.AddThemeFontSizeOverride("font_size", FontMd);
        return c;
    }

    private static void AddSlider(VBoxContainer vbox, string label, int min, int max, int initial, System.Action<double> onChange)
    {
        vbox.AddChild(MakeLabel(label));
        var slider = new HSlider { MinValue = min, MaxValue = max, Value = initial, CustomMinimumSize = new Vector2(300, 30) };
        slider.ValueChanged += (val) => onChange(val);
        vbox.AddChild(slider);
    }

    private static VBoxContainer MakeTabPage(string title)
    {
        var vbox = new VBoxContainer();
        vbox.Name = title;
        vbox.AddThemeConstantOverride("separation", 8);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        vbox.AddChild(margin);
        // VBoxContainer acts as both the tab page and the content container
        return vbox;
    }

    private static TabContentHelper MakeTabContent(string title)
    {
        var scroll = new ScrollContainer();
        scroll.Name = title;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        scroll.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        return new TabContentHelper(scroll, vbox);
    }

    /// <summary>Helper for tab content with scroll + vbox</summary>
    private class TabContentHelper
    {
        public ScrollContainer Scroll { get; }
        public VBoxContainer Vbox { get; }

        public TabContentHelper(ScrollContainer scroll, VBoxContainer vbox)
        {
            Scroll = scroll;
            Vbox = vbox;
        }

        public void AddChild(Node node) => Vbox.AddChild(node);
    }

    private static void AddSlider(TabContentHelper tab, string label, int min, int max, int initial, System.Action<double> onChange)
    {
        tab.Vbox.AddChild(MakeLabel(label));
        var slider = new HSlider { MinValue = min, MaxValue = max, Value = initial, CustomMinimumSize = new Vector2(300, 30) };
        slider.ValueChanged += (val) => onChange(val);
        tab.Vbox.AddChild(slider);
    }
}
