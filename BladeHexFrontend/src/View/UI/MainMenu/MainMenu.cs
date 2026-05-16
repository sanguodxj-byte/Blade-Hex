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
        var audio = BladeHex.Data.Globals.AudioOrNull;
        if (audio != null)
        {
            audio.PlayScenarioBgm(0, "default", 2.0f); 
        }

        // 30% 概率下雨打雷
        _rng.Randomize();
        _isRainyMenu = _rng.Randf() < 0.3f;

        _SetupUI();

        if (_isRainyMenu)
            _SetupRainEffect();
        else
        {
            // 确保非下雨时停止可能残留的雨声
            BladeHex.Data.Globals.AudioOrNull?.StopAmbient("ambient_rain", 1.0f);
        }
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
        var gs = BladeHex.Data.Globals.State;
        
        switch (action)
        {
            case "new_game":
                gs.Save.IsLoadingSave = false;
                gs.WorldGen.IsQuickGame = false;
                BladeHex.View.SceneTransition.ChangeSceneTo(GetTree(), "res://src/ui/main_menu/origin_select.tscn");
                break;
            case "continue":
                ShowSaveManagementPanel();
                break;
            case "quick_game":
                gs.Save.IsLoadingSave = false;
                gs.WorldGen.IsQuickGame = true;
                gs.OriginContext.Data = new Godot.Collections.Dictionary();
                LoadingScreen.LoadScene("res://src/scenes/overworld/overworld_scene_3d.tscn", LoadingScreen.PhaseType.QuickGame);
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
        var gs = BladeHex.Data.Globals.State;
        gs.Save.IsLoadingSave = true;
        gs.WorldGen.IsQuickGame = false;
        gs.Save.CurrentSaveId = saveId;
        LoadingScreen.LoadScene("res://src/scenes/overworld/overworld_scene_3d.tscn", LoadingScreen.PhaseType.LoadSave);
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
        // 统一使用 GameMenuManager 的设置面板（最完整版本：4 Tab 页）
        var gameMenu = BladeHex.Data.Globals.GameMenuOrNull;
        if (gameMenu != null)
        {
            gameMenu.IsInMainMenu = true;
            gameMenu.OpenSettings();
        }
    }

    // ========================================
    // 雨天氛围效果
    // ========================================

    private CpuParticles2D? _rainFx;
    private ColorRect? _lightningRect;
    private ShaderMaterial? _lightningMat;
    private AudioStreamPlayer? _thunderAudio;
    private float _thunderTimer;
    private float _lightningFade;
    private bool _lightningActive;
    private bool _isRainyMenu;
    private RandomNumberGenerator _rng = new();

    private void _SetupRainEffect()
    {
        var vp = new Vector2(1920, 1080);

        // 雨粒子（插入到背景之上、UI 之下）
        _rainFx = new CpuParticles2D();
        _rainFx.Name = "MenuRain";
        _rainFx.ZIndex = -1; // 在默认 UI 元素之下
        _rainFx.Amount = 250;
        _rainFx.Lifetime = 0.8f;
        _rainFx.Preprocess = 0.4f;
        _rainFx.Emitting = true;
        _rainFx.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
        _rainFx.EmissionRectExtents = new Vector2(vp.X * 0.6f, 5);
        _rainFx.Position = new Vector2(vp.X * 0.5f, -20);
        _rainFx.Direction = new Vector2(0.15f, 1);
        _rainFx.Spread = 5;
        _rainFx.InitialVelocityMin = 800;
        _rainFx.InitialVelocityMax = 1200;
        _rainFx.Gravity = new Vector2(30, 300);
        _rainFx.ScaleAmountMin = 0.8f;
        _rainFx.ScaleAmountMax = 1.2f;
        _rainFx.Color = new Color(0.6f, 0.7f, 0.85f, 0.3f);

        var rainImg = Image.CreateEmpty(2, 14, false, Image.Format.Rgba8);
        rainImg.Fill(new Color(0.75f, 0.82f, 0.95f, 0.5f));
        _rainFx.Texture = ImageTexture.CreateFromImage(rainImg);

        // 插入到第 1 个位置（背景是第 0 个）
        AddChild(_rainFx);
        MoveChild(_rainFx, 1);

        // 闪电 shader（缩小区域，只覆盖上半部分背景）
        _lightningRect = new ColorRect();
        _lightningRect.Name = "LightningFX";
        _lightningRect.ZIndex = -1; // 在 UI 之下
        // 只覆盖屏幕上部 60% 区域（闪电在天空中）
        _lightningRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _lightningRect.OffsetBottom = vp.Y * 0.55f;
        _lightningRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        _lightningRect.Color = Colors.White;

        var shader = GD.Load<Shader>("res://src/assets/shaders/lightning.gdshader");
        if (shader != null)
        {
            _lightningMat = new ShaderMaterial();
            _lightningMat.Shader = shader;
            _lightningMat.SetShaderParameter("fade", 0.0f);
            _lightningMat.SetShaderParameter("bolt_intensity", 2.0f);
            _lightningMat.SetShaderParameter("bolt_width", 0.015f);
            _lightningMat.SetShaderParameter("glow_radius", 0.18f);
            _lightningMat.SetShaderParameter("glow_intensity", 0.7f);
            _lightningRect.Material = _lightningMat;
        }
        _lightningRect.Visible = false;
        AddChild(_lightningRect);
        MoveChild(_lightningRect, 2);

        // 雨声（使用 AudioManager 的 ambient 系统，和大地图共用同一音效）
        var audio = BladeHex.Data.Globals.AudioOrNull;
        if (audio != null)
        {
            audio.PlayAmbient("ambient_rain", -10.0f);
        }

        // 雷声（使用 AudioManager 播放 SFX）
        _thunderAudio = new AudioStreamPlayer();
        _thunderAudio.Name = "ThunderSfx";
        _thunderAudio.VolumeDb = -4.0f;
        AddChild(_thunderAudio);

        _thunderTimer = _rng.RandfRange(3.0f, 8.0f);
    }

    public override void _Process(double delta)
    {
        if (!_isRainyMenu) return;
        float dt = (float)delta;

        // 闪电淡出
        if (_lightningActive)
        {
            _lightningFade -= dt * 2.5f;
            if (_lightningFade <= 0)
            {
                _lightningFade = 0;
                _lightningActive = false;
                if (_lightningRect != null) _lightningRect.Visible = false;
            }
            _lightningMat?.SetShaderParameter("fade", _lightningFade);
        }

        // 雷电计时
        _thunderTimer -= dt;
        if (_thunderTimer <= 0)
        {
            TriggerLightning();
            _thunderTimer = _rng.RandfRange(6.0f, 15.0f);
        }
    }

    private void TriggerLightning()
    {
        if (_lightningRect == null || _lightningMat == null) return;

        // 随机闪电位置和方向
        float startX = _rng.RandfRange(0.15f, 0.85f);
        float endX = startX + _rng.RandfRange(-0.15f, 0.15f);
        _lightningMat.SetShaderParameter("bolt_start", new Vector2(startX, 0.0f));
        _lightningMat.SetShaderParameter("bolt_end", new Vector2(endX, _rng.RandfRange(0.6f, 0.95f)));
        _lightningMat.SetShaderParameter("time_offset", _rng.Randf() * 100.0f);
        _lightningMat.SetShaderParameter("bolt_intensity", _rng.RandfRange(1.5f, 2.5f));
        _lightningMat.SetShaderParameter("branch_probability", _rng.RandfRange(0.3f, 0.7f));

        // 激活
        _lightningFade = 1.0f;
        _lightningActive = true;
        _lightningRect.Visible = true;
        _lightningMat.SetShaderParameter("fade", 1.0f);

        // 雷声延迟
        float thunderDelay = _rng.RandfRange(0.2f, 1.2f);
        var timer = GetTree().CreateTimer(thunderDelay);
        timer.Timeout += () =>
        {
            if (_thunderAudio == null || !IsInstanceValid(_thunderAudio)) return;
            var thunderStream = GD.Load<AudioStream>("res://assets/audio/sfx/thunder.ogg");
            if (thunderStream == null)
                thunderStream = GD.Load<AudioStream>("res://assets/audio/sfx/thunder.wav");
            if (thunderStream != null)
            {
                _thunderAudio.Stream = thunderStream;
                _thunderAudio.VolumeDb = _rng.RandfRange(-6.0f, 0.0f);
                _thunderAudio.Play();
            }
        };
    }
}
