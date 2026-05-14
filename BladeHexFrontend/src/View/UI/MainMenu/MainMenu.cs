// MainMenu.cs
// 游戏主入口界面 — 优化适配与居中布局
using Godot;
using BladeHex.Data;
using BladeHex.UI.Loading;

namespace BladeHex.UI;

[GlobalClass]
public partial class MainMenu : CanvasLayer
{
    private const string VersionString = "v0.2.1-Alpha";

    private UITheme Theme => UITheme.Instance!;
    private UIFactory _factory = null!;

    public override void _Ready()
    {
        _factory = new UIFactory();
        
        // 播放主菜单背景音乐
        var audio = GetNodeOrNull<BladeHex.Audio.AudioManager>("/root/AudioManager");
        if (audio != null)
        {
            audio.PlayScenarioBgm(0, "default", 2.0f); 
        }

        _SetupUI();
    }

    // ============================================================================
    // 资产路径
    // ============================================================================
    private const string BgTexturePath = "res://assets/generated_ui_main/selected/MainMenu_Background.png";
    private const string TitleTexturePath = "res://assets/generated_ui_main/selected/MainMenu_Title_steel_elegant_bright.png";
    private const string ButtonTexturePath = "res://assets/generated_ui_main/selected/Button_Parchment_v5.png";

    private void _SetupUI()
    {
        // 1. 背景图（替代纯色）
        var bgTex = GD.Load<Texture2D>(BgTexturePath);
        if (bgTex != null)
        {
            GD.Print($"[MainMenu] 背景纹理加载成功: {BgTexturePath}");
            var bgRect = new TextureRect();
            bgRect.Texture = bgTex;
            bgRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            bgRect.OffsetLeft = -10;
            bgRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            bgRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            AddChild(bgRect);
        }
        else
        {
            GD.PrintErr($"[MainMenu] 背景纹理加载失败: {BgTexturePath}，使用纯色回退");
            // 回退：纯色背景
            var bg = new ColorRect();
            bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            bg.Color = new Color(0.04f, 0.04f, 0.06f);
            AddChild(bg);
        }

        // 2. 全屏边距容器
        var mainMargin = new MarginContainer();
        mainMargin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mainMargin.AddThemeConstantOverride("margin_left", 80);
        mainMargin.AddThemeConstantOverride("margin_right", 80);
        mainMargin.AddThemeConstantOverride("margin_top", 60);
        mainMargin.AddThemeConstantOverride("margin_bottom", 60);
        AddChild(mainMargin);

        // 3. 版本号
        var versionLabel = new Label();
        versionLabel.Text = VersionString;
        versionLabel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        versionLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        versionLabel.Modulate = new Color(1, 1, 1, 0.4f);
        mainMargin.AddChild(versionLabel);

        // 4. 版权信息
        var footer = new Label();
        footer.Text = "© 2026 剑与六芒星 Sword & Hex. 保留所有权利。";
        footer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        footer.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        footer.Modulate = new Color(1, 1, 1, 0.3f);
        mainMargin.AddChild(footer);

        // 5. 核心内容
        var centerCont = new CenterContainer();
        centerCont.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mainMargin.AddChild(centerCont);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 60);
        centerCont.AddChild(mainVbox);

        // 标题：优先用生成的图片，回退到文字
        var titleTex = GD.Load<Texture2D>(TitleTexturePath);
        if (titleTex != null)
        {
            var titleRect = new TextureRect();
            titleRect.Texture = titleTex;
            titleRect.CustomMinimumSize = new Vector2(900, 280);
            titleRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            titleRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            titleRect.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            // 用 Additive 混合去掉黑底
            var titleMat = new CanvasItemMaterial();
            titleMat.BlendMode = CanvasItemMaterial.BlendModeEnum.Add;
            titleRect.Material = titleMat;
            mainVbox.AddChild(titleRect);
        }
        else
        {
            // 回退：文字标题
            var titleVbox = new VBoxContainer();
            titleVbox.AddThemeConstantOverride("separation", 15);
            mainVbox.AddChild(titleVbox);

            var titleLabel = new Label();
            titleLabel.Text = "剑 与 六 芒 星";
            titleLabel.AddThemeFontSizeOverride("font_size", 110);
            titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.6f));
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            titleVbox.AddChild(titleLabel);

            var subtitle = new Label();
            subtitle.Text = "SWORD & HEX";
            subtitle.AddThemeFontSizeOverride("font_size", 32);
            subtitle.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            titleVbox.AddChild(subtitle);
        }

        var menuVbox = new VBoxContainer();
        menuVbox.AddThemeConstantOverride("separation", 18);
        menuVbox.CustomMinimumSize = new Vector2(250, 0);
        menuVbox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        mainVbox.AddChild(menuVbox);

        _CreateMenuButton("新的起点", "new_game", menuVbox);
        _CreateMenuButton("继续旅程", "continue", menuVbox);
        _CreateMenuButton("快速游戏", "quick_game", menuVbox);
        _CreateMenuButton("快速战斗", "quick_combat", menuVbox);
        _CreateMenuButton("设置", "settings", menuVbox);
        _CreateMenuButton("退出", "exit", menuVbox);
    }

    private Button _CreateMenuButton(string text, string action, Control parent)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(180, 40);
        btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        btn.AddThemeFontSizeOverride("font_size", 24);
        btn.AddThemeColorOverride("font_color", new Color(0.15f, 0.12f, 0.08f)); // 深棕墨色文字
        btn.AddThemeColorOverride("font_hover_color", new Color(0.08f, 0.06f, 0.03f));
        btn.AddThemeColorOverride("font_pressed_color", new Color(0.3f, 0.25f, 0.18f));

        // 尝试加载羊皮纸按钮纹理
        var btnTex = GD.Load<Texture2D>(ButtonTexturePath);
        if (btnTex != null)
        {
            var styleNormal = new StyleBoxTexture();
            styleNormal.Texture = btnTex;
            styleNormal.ModulateColor = new Color(0.75f, 0.72f, 0.65f); // 压暗羊皮纸
            styleNormal.SetContentMarginAll(10);
            btn.AddThemeStyleboxOverride("normal", styleNormal);

            // Hover: 同纹理，微调 modulate
            var styleHover = (StyleBoxTexture)styleNormal.Duplicate();
            styleHover.ModulateColor = new Color(0.88f, 0.84f, 0.75f); // hover提亮一点
            btn.AddThemeStyleboxOverride("hover", styleHover);

            // Pressed: 同纹理，压暗
            var stylePressed = (StyleBoxTexture)styleNormal.Duplicate();
            stylePressed.ModulateColor = new Color(0.8f, 0.78f, 0.72f); // 压暗
            btn.AddThemeStyleboxOverride("pressed", stylePressed);
        }
        else
        {
            // 回退：纯色样式
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);
            style.SetBorderWidthAll(2);
            style.BorderColor = new Color(0.4f, 0.35f, 0.25f);
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusBottomRight = 4;
            btn.AddThemeStyleboxOverride("normal", style);

            var hover = (StyleBoxFlat)style.Duplicate();
            hover.BgColor = new Color(0.2f, 0.18f, 0.15f, 0.8f);
            hover.BorderColor = new Color(0.9f, 0.8f, 0.5f);
            btn.AddThemeStyleboxOverride("hover", hover);

            btn.AddThemeColorOverride("font_color", new Color(0.92f, 0.9f, 0.85f));
            btn.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.9f, 0.6f));
        }

        btn.Pressed += () => _OnMenuButtonPressed(action);
        
        btn.Pressed += () => {
            BladeHex.Audio.AudioManager.Instance?.PlaySfxName("ui_click");
        };
        btn.MouseEntered += () => {
            BladeHex.Audio.AudioManager.Instance?.PlaySfxName("ui_hover", -6.0f);
        };

        parent.AddChild(btn);
        return btn;
    }

    private void _OnMenuButtonPressed(string action)
    {
        var gs = GetNode<GlobalState>("/root/GlobalState");
        
        switch (action)
        {
            case "new_game":
                gs.IsLoadingSave = false;
                gs.IsQuickGame = false;
                BladeHex.View.SceneTransition.ChangeSceneTo(GetTree(), "res://src/ui/main_menu/origin_select.tscn");
                break;
            case "continue":
                ShowSaveManagementPanel();
                break;
            case "quick_game":
                gs.IsLoadingSave = false;
                gs.IsQuickGame = true;
                gs.PlayerOrigin = new Godot.Collections.Dictionary();
                LoadingScreen.LoadScene("res://src/scenes/overworld/overworld_scene.tscn", LoadingScreen.PhaseType.QuickGame);
                break;
            case "quick_combat":
                ShowQuickCombatSetup();
                break;
            case "settings":
                ShowSettingsPanel();
                break;
            case "exit":
                GetTree().Quit();
                break;
        }
    }

    // ========================================
    // 存档管理面板
    // ========================================

    private PanelContainer? _savePanel;

    private void ShowSaveManagementPanel()
    {
        // 如果已存在，切换可见性
        if (_savePanel != null)
        {
            _savePanel.Visible = !_savePanel.Visible;
            if (_savePanel.Visible) RefreshSaveList();
            return;
        }

        // 创建存档管理面板
        _savePanel = new PanelContainer();
        _savePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _savePanel.ZIndex = 100;

        var bg = new StyleBoxFlat { BgColor = new Color(0.0f, 0.0f, 0.0f, 0.75f) };
        bg.SetBorderWidthAll(0);
        _savePanel.AddThemeStyleboxOverride("panel", bg);
        AddChild(_savePanel);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _savePanel.AddChild(center);

        var inner = new PanelContainer();
        var innerBg = new StyleBoxFlat { BgColor = new Color(0.06f, 0.06f, 0.08f, 0.95f) };
        innerBg.SetBorderWidthAll(2);
        innerBg.BorderColor = new Color(0.5f, 0.45f, 0.3f);
        innerBg.SetCornerRadiusAll(8);
        innerBg.SetContentMarginAll(30);
        inner.AddThemeStyleboxOverride("panel", innerBg);
        inner.CustomMinimumSize = new Vector2(500, 400);
        center.AddChild(inner);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        inner.AddChild(vbox);

        var title = new Label { Text = "存 档 管 理" };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.6f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        // 存档列表滚动容器
        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(440, 280);
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(scroll);

        _saveListContainer = new VBoxContainer();
        _saveListContainer.AddThemeConstantOverride("separation", 8);
        _saveListContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_saveListContainer);

        // 底部按钮
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 16);
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(btnRow);

        var closeBtn = new Button { Text = "返 回" };
        closeBtn.CustomMinimumSize = new Vector2(120, 45);
        closeBtn.AddThemeFontSizeOverride("font_size", 18);
        closeBtn.Pressed += () => { _savePanel.Visible = false; };
        btnRow.AddChild(closeBtn);

        RefreshSaveList();
    }

    private VBoxContainer? _saveListContainer;

    /// <summary>刷新存档列表</summary>
    private void RefreshSaveList()
    {
        if (_saveListContainer == null) return;

        // 立即移除并释放旧列表项
        foreach (var child in _saveListContainer.GetChildren())
        {
            _saveListContainer.RemoveChild(child);
            child.QueueFree();
        }

        // 扫描存档目录
        var saves = ScanSaveDirectories();

        if (saves.Count == 0)
        {
            var emptyLabel = new Label { Text = "暂无存档" };
            emptyLabel.AddThemeFontSizeOverride("font_size", 16);
            emptyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _saveListContainer.AddChild(emptyLabel);
            return;
        }

        foreach (var save in saves)
        {
            var row = CreateSaveRow(save);
            _saveListContainer.AddChild(row);
        }
    }

    /// <summary>创建单个存档行</summary>
    private Control CreateSaveRow(SaveInfo save)
    {
        var panel = new PanelContainer();
        var panelBg = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.14f, 0.8f) };
        panelBg.SetBorderWidthAll(1);
        panelBg.BorderColor = new Color(0.3f, 0.3f, 0.35f);
        panelBg.SetCornerRadiusAll(4);
        panelBg.SetContentMarginAll(12);
        panel.AddThemeStyleboxOverride("panel", panelBg);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(hbox);

        // 存档信息
        var infoVbox = new VBoxContainer();
        infoVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        infoVbox.AddThemeConstantOverride("separation", 4);
        hbox.AddChild(infoVbox);

        var nameLabel = new Label { Text = save.DisplayName };
        nameLabel.AddThemeFontSizeOverride("font_size", 16);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.8f));
        infoVbox.AddChild(nameLabel);

        var detailLabel = new Label { Text = save.DetailText };
        detailLabel.AddThemeFontSizeOverride("font_size", 12);
        detailLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        infoVbox.AddChild(detailLabel);

        // 加载按钮
        var loadBtn = new Button { Text = "进入" };
        loadBtn.CustomMinimumSize = new Vector2(70, 36);
        loadBtn.AddThemeFontSizeOverride("font_size", 14);
        loadBtn.Pressed += () => LoadSave(save.SaveId);
        hbox.AddChild(loadBtn);

        // 删除按钮
        var delBtn = new Button { Text = "删除" };
        delBtn.CustomMinimumSize = new Vector2(70, 36);
        delBtn.AddThemeFontSizeOverride("font_size", 14);
        delBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
        delBtn.Pressed += () => ConfirmDeleteSave(save.SaveId);
        hbox.AddChild(delBtn);

        return panel;
    }

    /// <summary>加载指定存档</summary>
    private void LoadSave(string saveId)
    {
        var gs = GetNode<GlobalState>("/root/GlobalState");
        gs.IsLoadingSave = true;
        gs.IsQuickGame = false;
        gs.CurrentSaveId = saveId;
        LoadingScreen.LoadScene("res://src/scenes/overworld/overworld_scene.tscn", LoadingScreen.PhaseType.LoadSave);
    }

    /// <summary>确认删除存档</summary>
    private void ConfirmDeleteSave(string saveId)
    {
        // 简易确认：直接删除（后续可加确认对话框）
        BladeHex.Map.ChunkPersistence.DeleteSave(saveId);
        GD.Print($"[MainMenu] 已删除存档: {saveId}");
        RefreshSaveList();
    }

    /// <summary>扫描所有存档目录</summary>
    private static System.Collections.Generic.List<SaveInfo> ScanSaveDirectories()
    {
        var saves = new System.Collections.Generic.List<SaveInfo>();
        string savesRoot = "user://saves";

        var dir = DirAccess.Open(savesRoot);
        if (dir == null) return saves;

        dir.ListDirBegin();
        string dirName = dir.GetNext();
        while (!string.IsNullOrEmpty(dirName))
        {
            if (dir.CurrentIsDir() && dirName != "." && dirName != "..")
            {
                string saveId = dirName;
                // 检查是否有有效的世界数据
                if (BladeHex.Map.ChunkPersistence.HasSave(saveId))
                {
                    var info = new SaveInfo { SaveId = saveId };

                    // 尝试读取元数据
                    var meta = BladeHex.Map.ChunkPersistence.LoadWorldMeta(saveId);
                    if (meta != null)
                    {
                        int seed = meta.ContainsKey("seed") ? (int)meta["seed"] : 0;
                        int poiCount = meta.ContainsKey("poi_count") ? (int)meta["poi_count"] : 0;
                        info.DisplayName = $"世界 (种子: {seed})";
                        info.DetailText = $"{poiCount} 个据点 | {saveId}";
                    }
                    else
                    {
                        info.DisplayName = saveId;
                        info.DetailText = "存档数据";
                    }

                    saves.Add(info);
                }
            }
            dirName = dir.GetNext();
        }
        dir.ListDirEnd();

        return saves;
    }

    /// <summary>存档信息</summary>
    private class SaveInfo
    {
        public string SaveId { get; set; } = "";
        public string DisplayName { get; set; } = "未知存档";
        public string DetailText { get; set; } = "";
    }

    // ========================================
    // 快速战斗配置
    // ========================================

    private QuickCombatSetup? _quickCombatSetup;

    private void ShowQuickCombatSetup()
    {
        if (_quickCombatSetup != null)
        {
            _quickCombatSetup.ShowPanel();
            return;
        }

        _quickCombatSetup = new QuickCombatSetup();
        AddChild(_quickCombatSetup);
        _quickCombatSetup.ShowPanel();

        _quickCombatSetup.StartCombat += () =>
        {
            BladeHex.Debug.GameLog.Info("[MainMenu] 快速战斗启动...");
            _quickCombatSetup.QueueFree();
            _quickCombatSetup = null;

            try
            {
                var script = GD.Load<CSharpScript>("res://BladeHexFrontend/src/Scenes/combat/QuickCombatScene.cs");
                if (script == null)
                {
                    BladeHex.Debug.GameLog.Err("[MainMenu] 无法加载 QuickCombatScene.cs");
                    return;
                }

                var obj = script.New();
                if (obj.Obj == null)
                {
                    BladeHex.Debug.GameLog.Err("[MainMenu] QuickCombatScene.New() 返回 null");
                    return;
                }

                var scene = (Node)obj.AsGodotObject();
                GetTree().Root.AddChild(scene);
                Visible = false;
                BladeHex.Debug.GameLog.Info("[MainMenu] 快速战斗场景已启动 ✓");
            }
            catch (System.Exception ex)
            {
                BladeHex.Debug.GameLog.Exception("[MainMenu] 快速战斗启动异常", ex);
            }
        };

        _quickCombatSetup.BackPressed += () => { };
    }

    // ========================================
    // 设置面板
    // ========================================

    private PanelContainer? _settingsPanel;

    private void ShowSettingsPanel()
    {
        if (_settingsPanel != null)
        {
            _settingsPanel.Visible = !_settingsPanel.Visible;
            return;
        }

        _settingsPanel = new PanelContainer();
        _settingsPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _settingsPanel.ZIndex = 100;

        var bg = new StyleBoxFlat { BgColor = new Color(0.0f, 0.0f, 0.0f, 0.75f) };
        bg.SetBorderWidthAll(0);
        _settingsPanel.AddThemeStyleboxOverride("panel", bg);
        AddChild(_settingsPanel);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _settingsPanel.AddChild(center);

        var inner = new PanelContainer();
        inner.CustomMinimumSize = new Vector2(550, 0);
        var innerBg = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.1f, 0.96f) };
        innerBg.SetBorderWidthAll(2);
        innerBg.BorderColor = new Color(0.5f, 0.45f, 0.3f);
        innerBg.SetCornerRadiusAll(10);
        innerBg.SetContentMarginAll(30);
        inner.AddThemeStyleboxOverride("panel", innerBg);
        center.AddChild(inner);

        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(500, 550);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        inner.AddChild(scroll);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(vbox);

        // === 标题 ===
        var title = new Label { Text = "设 置" };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.6f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);
        vbox.AddChild(MakeSettingsSeparator());

        // === 音频 ===
        AddSectionTitle(vbox, "音频");

        AddSliderSetting(vbox, "主音量", 0, 100, 80, (val) =>
        {
            float db = val > 0 ? Mathf.LinearToDb((float)val / 100.0f) : -80.0f;
            AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), db);
        });

        AddSliderSetting(vbox, "音乐音量", 0, 100, 70, (val) =>
        {
            int idx = AudioServer.GetBusIndex("Music");
            if (idx >= 0)
            {
                float db = val > 0 ? Mathf.LinearToDb((float)val / 100.0f) : -80.0f;
                AudioServer.SetBusVolumeDb(idx, db);
            }
        });

        AddSliderSetting(vbox, "音效音量", 0, 100, 80, (val) =>
        {
            int idx = AudioServer.GetBusIndex("SFX");
            if (idx >= 0)
            {
                float db = val > 0 ? Mathf.LinearToDb((float)val / 100.0f) : -80.0f;
                AudioServer.SetBusVolumeDb(idx, db);
            }
        });

        vbox.AddChild(MakeSettingsSeparator());

        // === 显示 ===
        AddSectionTitle(vbox, "显示");

        var fullscreenCheck = MakeCheckbox("全屏模式",
            DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen);
        fullscreenCheck.Toggled += (pressed) =>
        {
            DisplayServer.WindowSetMode(pressed
                ? DisplayServer.WindowMode.Fullscreen
                : DisplayServer.WindowMode.Windowed);
        };
        vbox.AddChild(fullscreenCheck);

        var vsyncCheck = MakeCheckbox("垂直同步",
            DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled);
        vsyncCheck.Toggled += (pressed) =>
        {
            DisplayServer.WindowSetVsyncMode(pressed
                ? DisplayServer.VSyncMode.Enabled
                : DisplayServer.VSyncMode.Disabled);
        };
        vbox.AddChild(vsyncCheck);

        // 分辨率选择
        var resLabel = MakeSettingsLabel("窗口分辨率");
        vbox.AddChild(resLabel);
        var resOption = new OptionButton { CustomMinimumSize = new Vector2(250, 38) };
        resOption.AddThemeFontSizeOverride("font_size", 15);
        resOption.AddItem("1280 × 720", 0);
        resOption.AddItem("1600 × 900", 1);
        resOption.AddItem("1920 × 1080", 2);
        resOption.AddItem("2560 × 1440", 3);
        // 选中当前分辨率
        var curSize = DisplayServer.WindowGetSize();
        if (curSize.X >= 2560) resOption.Selected = 3;
        else if (curSize.X >= 1920) resOption.Selected = 2;
        else if (curSize.X >= 1600) resOption.Selected = 1;
        else resOption.Selected = 0;
        resOption.ItemSelected += (idx) =>
        {
            Vector2I size = idx switch
            {
                1 => new Vector2I(1600, 900),
                2 => new Vector2I(1920, 1080),
                3 => new Vector2I(2560, 1440),
                _ => new Vector2I(1280, 720),
            };
            DisplayServer.WindowSetSize(size);
            // 居中窗口
            var screenSize = DisplayServer.ScreenGetSize();
            DisplayServer.WindowSetPosition((screenSize - size) / 2);
        };
        vbox.AddChild(resOption);

        vbox.AddChild(MakeSettingsSeparator());

        // === 游戏 ===
        AddSectionTitle(vbox, "游戏");

        AddSliderSetting(vbox, "游戏速度", 1, 5, 2, (val) =>
        {
            var gs = GetNodeOrNull<GlobalState>("/root/GlobalState");
            if (gs != null) gs.Set("game_speed_multiplier", (float)val);
        });

        AddSliderSetting(vbox, "自动存档间隔 (分钟)", 1, 30, 5, (val) =>
        {
            var gs = GetNodeOrNull<GlobalState>("/root/GlobalState");
            if (gs != null) gs.Set("autosave_interval_min", (int)val);
        });

        var showDamageNumbers = MakeCheckbox("显示伤害数字", true);
        showDamageNumbers.Toggled += (pressed) =>
        {
            var gs = GetNodeOrNull<GlobalState>("/root/GlobalState");
            if (gs != null) gs.Set("show_damage_numbers", pressed);
        };
        vbox.AddChild(showDamageNumbers);

        var showTutorial = MakeCheckbox("显示教程提示", true);
        showTutorial.Toggled += (pressed) =>
        {
            var gs = GetNodeOrNull<GlobalState>("/root/GlobalState");
            if (gs != null) gs.Set("show_tutorials", pressed);
        };
        vbox.AddChild(showTutorial);

        var cameraShake = MakeCheckbox("战斗镜头震动", true);
        cameraShake.Toggled += (pressed) =>
        {
            var gs = GetNodeOrNull<GlobalState>("/root/GlobalState");
            if (gs != null) gs.Set("camera_shake_enabled", pressed);
        };
        vbox.AddChild(cameraShake);

        vbox.AddChild(MakeSettingsSeparator());

        // === 关闭按钮 ===
        var closeBtn = new Button { Text = "返 回" };
        closeBtn.CustomMinimumSize = new Vector2(180, 48);
        closeBtn.AddThemeFontSizeOverride("font_size", 18);
        var closeBtnStyle = new StyleBoxFlat { BgColor = new Color(0.15f, 0.14f, 0.18f) };
        closeBtnStyle.SetBorderWidthAll(1);
        closeBtnStyle.BorderColor = new Color(0.4f, 0.35f, 0.28f);
        closeBtnStyle.SetCornerRadiusAll(6);
        closeBtnStyle.SetContentMarginAll(8);
        closeBtn.AddThemeStyleboxOverride("normal", closeBtnStyle);
        closeBtn.Pressed += () => { _settingsPanel.Visible = false; };
        vbox.AddChild(closeBtn);
    }

    // === 设置面板辅助方法 ===

    private static void AddSectionTitle(VBoxContainer parent, string text)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 20);
        lbl.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.5f));
        parent.AddChild(lbl);
    }

    private static Label MakeSettingsLabel(string text)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 15);
        lbl.AddThemeColorOverride("font_color", new Color(0.8f, 0.78f, 0.72f));
        return lbl;
    }

    private static void AddSliderSetting(VBoxContainer parent, string label, int min, int max, int defaultVal, System.Action<double> onChange)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        parent.AddChild(hbox);

        var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(200, 0) };
        lbl.AddThemeFontSizeOverride("font_size", 15);
        lbl.AddThemeColorOverride("font_color", new Color(0.8f, 0.78f, 0.72f));
        hbox.AddChild(lbl);

        var slider = new HSlider { MinValue = min, MaxValue = max, Value = defaultVal, CustomMinimumSize = new Vector2(200, 0) };
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(slider);

        var valLabel = new Label { Text = defaultVal.ToString(), CustomMinimumSize = new Vector2(40, 0) };
        valLabel.AddThemeFontSizeOverride("font_size", 15);
        valLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.6f));
        hbox.AddChild(valLabel);

        slider.ValueChanged += (val) =>
        {
            valLabel.Text = ((int)val).ToString();
            onChange(val);
        };
    }

    private static CheckBox MakeCheckbox(string text, bool defaultVal)
    {
        var cb = new CheckBox { Text = text, ButtonPressed = defaultVal };
        cb.AddThemeFontSizeOverride("font_size", 15);
        cb.AddThemeColorOverride("font_color", new Color(0.8f, 0.78f, 0.72f));
        return cb;
    }

    private static HSeparator MakeSettingsSeparator()
    {
        var sep = new HSeparator();
        var style = new StyleBoxFlat { BgColor = new Color(0.3f, 0.28f, 0.22f, 0.5f) };
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }
}
