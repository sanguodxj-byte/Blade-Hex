// OverworldUI.cs
// 大地图HUD覆盖层 — 包含顶部信息栏、底部功能栏、子面板、ESC菜单
// 对应策划：09-UI设计.md — 战略层UI
// 对应策划：04-战略层系统.md — 大地图/城镇/遭遇
using Godot;
using Godot.Collections;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Scenes.Overworld;
using BladeHex.UI;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class OverworldUI : CanvasLayer
{
    // ============================================================================
    // 信号
    // ============================================================================
    [Signal]
    public delegate void MenuOpenedEventHandler(string menuName);

    [Signal]
    public delegate void PartyClickedEventHandler();

    [Signal]
    public delegate void CharacterClickedEventHandler();

    [Signal]
    public delegate void InventoryClickedEventHandler();

    // ============================================================================
    // 主题常量
    // ============================================================================
    private static readonly Color BgPrimary = new(0.08f, 0.08f, 0.10f, 0.85f);
    private static readonly Color BgSecondary = new(0.12f, 0.12f, 0.14f, 0.80f);
    private static readonly Color BgTertiary = new(0.06f, 0.06f, 0.08f, 0.75f);
    private static readonly Color BgPanel = new(0.10f, 0.10f, 0.12f, 0.85f);
    private static readonly Color BgCard = new(0.15f, 0.14f, 0.18f, 0.75f);
    private static readonly Color BgCardHover = new(0.25f, 0.22f, 0.30f, 0.90f);
    private static readonly Color BorderDefault = new(0.3f, 0.3f, 0.35f, 0.6f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPositive = new(0.3f, 0.85f, 0.3f);
    private static readonly Color TextNegative = new(0.9f, 0.3f, 0.25f);
    private static readonly Color TextMagic = new(0.7f, 0.6f, 1.0f);
    private static readonly Color TextWarning = new(0.9f, 0.7f, 0.2f);

    private const int FontSizeXxl = 24;
    private const int FontSizeXl = 20;
    private const int FontSizeLg = 16;
    private const int FontSizeMd = 14;
    private const int FontSizeSm = 12;
    private const int FontSizeXs = 10;
    private const int SpacingMd = 8;
    private const int SpacingSm = 4;
    private const int SpacingLg = 12;
    private const int SpacingXl = 16;
    private const int SpacingXxl = 24;
    private const int RadiusMd = 8;
    private const int RadiusSm = 4;
    private const int RadiusLg = 12;
    private const int ButtonHeight = 36;
    private const int ButtonHeightLg = 45;

    // ============================================================================
    // 字段
    // ============================================================================
    private Control _root = null!;
    private Label _dayLabel = null!;
    private Label _goldLabel = null!;
    private Label _foodLabel = null!;
    private Label _speedStatusLabel = null!;   // 速度状态 (正常/急行/扎营)
    private Label _moraleStatusLabel = null!;  // 士气 (高昂/正常/低落)
    private Label _reputationLabel = null!;    // 声望
    private Label _speedLabel = null!;         // 季节
    private Label _moraleLabel = null!;        // 时间
    private Label _terrainLabel = null!;
    private Label _weatherLabel = null!;
    private HBoxContainer _bottomBar = null!;
    private PanelContainer _escMenu = null!;
    private PanelContainer _minimapContainer = null!;

    // 子面板
    private PartyPanel _partyPanel = null!;
    private TownUI _townUi = null!;

    // 外部系统引用 (由 OverworldScene 设置)
    public Node EconomyManager { get; set; } = null!;
    public SaveManagerV2 SaveMgr { get; private set; } = new();

    // 强类型场景上下文（替代 GetParent().Get("...") 反射访问）
    private IOverworldContext? _context;
    private UIFactory _factory = null!;

    // 暂未迁移的子面板（保留为字段，等后续转换）
    private Node _characterDetail = null!;
    private Node _skillTreeUi = null!;
    private Node _questLog = null!;
    private Node _settingsPanel = null!;

    // ============================================================================
    // 生命周期
    // ============================================================================

    public override void _Ready()
    {
        Layer = 10;
        _factory = new UIFactory();
        _SetupUi();
    }

    /// <summary>
    /// 延迟获取场景上下文（父节点在 _Ready 时可能尚未就绪）。
    /// 首次调用时缓存引用。
    /// </summary>
    private IOverworldContext? GetContext()
    {
        if (_context != null) return _context;
        _context = GetParent() as IOverworldContext;
        return _context;
    }

    // ============================================================================
    // UI构建
    // ============================================================================

    private void _SetupUi()
    {
        var root = new Control();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(root);
        _root = root;

        // ─── 1. 顶部信息栏 ───
        var topPanel = new PanelContainer();
        var topStyle = new StyleBoxFlat
        {
            BgColor = BgPanel,
            ShadowColor = new Color(0, 0, 0, 0.6f),
            ShadowSize = 6
        };
        topStyle.SetBorderWidthAll(2);
        topStyle.BorderColor = BorderHighlight;
        topPanel.AddThemeStyleboxOverride("panel", topStyle);
        topPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        topPanel.MouseFilter = Control.MouseFilterEnum.Pass;
        root.AddChild(topPanel);

        var topMargin = new MarginContainer();
        topMargin.AddThemeConstantOverride("margin_left", 20);
        topMargin.AddThemeConstantOverride("margin_right", 20);
        topMargin.AddThemeConstantOverride("margin_top", 6);
        topMargin.AddThemeConstantOverride("margin_bottom", 6);
        topPanel.AddChild(topMargin);

        var topHbox = new HBoxContainer();
        topHbox.AddThemeConstantOverride("separation", SpacingLg * 2);
        topMargin.AddChild(topHbox);

        _dayLabel = new Label();
        _dayLabel.Text = "\U0001f4c5 1250\u5e74 1\u6708 1\u65e5";
        _dayLabel.AddThemeColorOverride("font_color", TextAccent);
        _dayLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        topHbox.AddChild(_dayLabel);

        topHbox.AddChild(_CreateSeparatorV());

        _goldLabel = new Label();
        _goldLabel.Text = "\U0001f4b0 \u91d1\u5e01: 1000";
        _goldLabel.AddThemeColorOverride("font_color", TextAccent);
        _goldLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        topHbox.AddChild(_goldLabel);

        _foodLabel = new Label();
        _foodLabel.Text = "\U0001f356 \u98df\u7269: 20/40";
        _foodLabel.AddThemeColorOverride("font_color", TextSecondary);
        _foodLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        topHbox.AddChild(_foodLabel);

        // 新增：速度状态
        _speedStatusLabel = new Label();
        _speedStatusLabel.Text = "\u2694\ufe0f \u6b63\u5e38";
        _speedStatusLabel.AddThemeColorOverride("font_color", TextSecondary);
        _speedStatusLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        topHbox.AddChild(_speedStatusLabel);

        // 新增：士气
        _moraleStatusLabel = new Label();
        _moraleStatusLabel.Text = "\U0001f3ad \u6b63\u5e38";
        _moraleStatusLabel.AddThemeColorOverride("font_color", TextSecondary);
        _moraleStatusLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        topHbox.AddChild(_moraleStatusLabel);

        // 新增：声望
        _reputationLabel = new Label();
        _reputationLabel.Text = "\U0001f91d \u58f0\u671b:0";
        _reputationLabel.AddThemeColorOverride("font_color", TextSecondary);
        _reputationLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        topHbox.AddChild(_reputationLabel);

        _speedLabel = new Label();
        _speedLabel.Text = "\U0001f324 \u5b63\u8282: \u6625\u5b63";
        _speedLabel.AddThemeColorOverride("font_color", TextSecondary);
        _speedLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        topHbox.AddChild(_speedLabel);

        _moraleLabel = new Label();
        _moraleLabel.Text = "\u23f3 \u65f6\u95f4: 08:00";
        _moraleLabel.AddThemeColorOverride("font_color", TextSecondary);
        _moraleLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        topHbox.AddChild(_moraleLabel);

        // 右侧弹性间隔
        var topSpacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        topHbox.AddChild(topSpacer);

        // 地形显示（右上角）
        _terrainLabel = new Label();
        _terrainLabel.Text = "\U0001f30d 地形: ---";
        _terrainLabel.AddThemeColorOverride("font_color", TextSecondary);
        _terrainLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        topHbox.AddChild(_terrainLabel);

        // 天气显示
        _weatherLabel = new Label();
        _weatherLabel.Text = "☀ 晴天";
        _weatherLabel.AddThemeColorOverride("font_color", TextSecondary);
        _weatherLabel.AddThemeFontSizeOverride("font_size", FontSizeMd);
        topHbox.AddChild(_weatherLabel);

        // ─── 2. 底部功能栏 ───
        var bottomPanel = new PanelContainer();
        var botStyle = new StyleBoxFlat
        {
            BgColor = BgPanel,
            ShadowColor = new Color(0, 0, 0, 0.8f),
            ShadowSize = 10
        };
        botStyle.SetBorderWidthAll(3);
        botStyle.BorderColor = BorderHighlight;
        bottomPanel.AddThemeStyleboxOverride("panel", botStyle);
        bottomPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        bottomPanel.GrowVertical = Control.GrowDirection.Begin;
        bottomPanel.MouseFilter = Control.MouseFilterEnum.Pass;
        root.AddChild(bottomPanel);

        var bottomMargin = new MarginContainer();
        bottomMargin.AddThemeConstantOverride("margin_left", 20);
        bottomMargin.AddThemeConstantOverride("margin_right", 20);
        bottomMargin.AddThemeConstantOverride("margin_top", 12);
        bottomMargin.AddThemeConstantOverride("margin_bottom", 16);
        bottomPanel.AddChild(bottomMargin);

        _bottomBar = new HBoxContainer();
        _bottomBar.Alignment = BoxContainer.AlignmentMode.Center;
        _bottomBar.AddThemeConstantOverride("separation", SpacingLg);
        bottomMargin.AddChild(_bottomBar);

        // 6 buttons — party/inventory/army merged into unified 军队 panel
        _CreateBarButton("⚔️ 军队 [I]", "army", TextPrimary);
        _CreateBarButton("👤 角色 [C]", "character", TextPrimary);
        _CreateBarButton("✨ 技能盘 [K]", "skill_tree", TextMagic);
        _CreateBarButton("📜 任务 [J]", "quests", TextWarning);
        _CreateBarButton("⛺ 营地 [T]", "camp", TextPositive);
        _CreateBarButton("🏰 领地 [F]", "territory", TextSecondary);

        // ─── 3. 子面板初始化 ───
        _partyPanel = new PartyPanel();
        _partyPanel.Visible = false;
        root.AddChild(_partyPanel);

        _townUi = new TownUI();
        _townUi.Visible = false;
        root.AddChild(_townUi);

        // ─── 4. ESC 系统菜单 ───
        _escMenu = new PanelContainer();
        var escBg = new StyleBoxFlat
        {
            BgColor = new Color(0.0f, 0.0f, 0.0f, 0.6f)
        };
        escBg.SetBorderWidthAll(2);
        escBg.BorderColor = BorderHighlight;
        escBg.SetCornerRadiusAll(RadiusMd);
        _escMenu.AddThemeStyleboxOverride("panel", escBg);
        _escMenu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _escMenu.Visible = false;
        root.AddChild(_escMenu);

        var escCenter = new CenterContainer();
        escCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _escMenu.AddChild(escCenter);

        var escInner = new PanelContainer();
        var escInnerBg = new StyleBoxFlat
        {
            BgColor = BgPrimary
        };
        escInnerBg.SetBorderWidthAll(2);
        escInnerBg.BorderColor = BorderHighlight;
        escInnerBg.SetCornerRadiusAll(RadiusMd);
        escInnerBg.SetContentMarginAll(30);
        escInner.AddThemeStyleboxOverride("panel", escInnerBg);
        escCenter.AddChild(escInner);

        var escVbox = new VBoxContainer();
        escVbox.AddThemeConstantOverride("alignment", 1);
        escVbox.AddThemeConstantOverride("separation", SpacingLg);
        escVbox.CustomMinimumSize = new Vector2(220, 0);
        escInner.AddChild(escVbox);

        var escTitle = new Label();
        escTitle.Text = "- \u7cfb\u7edf\u83dc\u5355 -";
        escTitle.HorizontalAlignment = HorizontalAlignment.Center;
        escTitle.AddThemeFontSizeOverride("font_size", FontSizeXl);
        escTitle.AddThemeColorOverride("font_color", TextAccent);
        escVbox.AddChild(escTitle);

        _CreateEscButton("\u4fdd\u5b58\u6e38\u620f", "save", escVbox);
        _CreateEscButton("\u52a0\u8f7d\u6e38\u620f", "load", escVbox);
        _CreateEscButton("\u8bbe\u7f6e", "settings", escVbox);
        _CreateEscButton("\u8fd4\u56de\u6e38\u620f", "resume", escVbox);

        var mainMenuBtn = _CreateButton("\u56de\u5230\u4e3b\u83dc\u5355", new Vector2(200, ButtonHeightLg));
        mainMenuBtn.Pressed += () =>
        {
            _escMenu.Visible = false;
            BladeHex.View.SceneTransition.ChangeSceneTo(GetTree(), "res://src/ui/main_menu/main_menu.tscn");
        };
        escVbox.AddChild(mainMenuBtn);

        var exitBtn = _CreateButton("\u9000\u51fa\u6e38\u620f", new Vector2(200, ButtonHeightLg));
        exitBtn.AddThemeColorOverride("font_color", TextNegative);
        exitBtn.Pressed += () => { GetTree().Quit(); };
        escVbox.AddChild(exitBtn);
    }

    // ============================================================================
    // 按钮创建
    // ============================================================================

    private void _CreateBarButton(string text, string actionName, Color color)
    {
        var btn = _CreateButton(text, new Vector2(100, 42));
        btn.AddThemeFontSizeOverride("font_size", FontSizeMd);
        btn.AddThemeColorOverride("font_color", color);

        var normalStyle = new StyleBoxFlat
        {
            BgColor = BgSecondary
        };
        normalStyle.SetBorderWidthAll(2);
        normalStyle.BorderColor = BorderDefault;
        normalStyle.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("normal", normalStyle);

        var hoverStyle = new StyleBoxFlat
        {
            BgColor = BgCardHover
        };
        hoverStyle.SetBorderWidthAll(2);
        hoverStyle.BorderColor = BorderHighlight;
        hoverStyle.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);

        var pressedStyle = new StyleBoxFlat
        {
            BgColor = BgTertiary
        };
        pressedStyle.SetBorderWidthAll(2);
        pressedStyle.BorderColor = BorderHighlight;
        pressedStyle.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("pressed", pressedStyle);

        btn.Pressed += () => _OnButtonPressed(actionName);
        btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        _bottomBar.AddChild(btn);
    }

    private void _CreateEscButton(string text, string actionName, Control parent)
    {
        var btn = _CreateButton(text, new Vector2(200, ButtonHeightLg));
        btn.Pressed += () => _OnButtonPressed(actionName);
        parent.AddChild(btn);
    }

    // ============================================================================
    // 按钮处理
    // ============================================================================

    private void _OnButtonPressed(string actionName)
    {
        _escMenu.Visible = false;
        switch (actionName)
        {
            case "resume":
                break;
            case "save":
                if (EconomyManager is BladeHex.Data.EconomyManager econ)
                {
                    var ctx = GetContext();
                    var playerUnit = ctx?.PlayerUnitData;
                    var playerPos = ctx?.PlayerParty?.Position ?? Vector2.Zero;
                    int raceId = ctx?.PlayerRaceId ?? 0;

                    if (playerUnit != null)
                    {
                        var entityMgr = ctx?.EntityMgr;
                        var gs = GetNode<BladeHex.Data.GlobalState>("/root/GlobalState");
                        var saveData = SaveManagerV2.BuildSaveData(
                            playerUnit, raceId, playerPos, econ, entityMgr,
                            gs?.WorldSeed ?? 0, gs?.WorldSize ?? 1, gs?.CurrentSaveId);
                        SaveMgr.SaveGame(saveData);
                    }

                    // 持久化世界 chunk 数据（只有手动保存时才写磁盘）
                    ctx?.SaveWorldData();
                }
                break;
            case "load":
                EmitSignal(SignalName.MenuOpened, "load_game");
                break;
            case "party":
            case "inventory":
            case "army":
                _CloseAllPanels();
                _OpenPartyPanel();
                break;
            case "character":
                _CloseAllPanels();
                _OpenCharacterDetail();
                break;
            case "quests":
                _CloseAllPanels();
                _OpenQuestLog();
                break;
            case "camp":
                {
                    var ctx = GetContext();
                    if (ctx != null)
                    {
                        bool isWaiting = !ctx.IsWaiting;
                        ctx.IsWaiting = isWaiting;
                        if (isWaiting && ctx.PlayerParty != null)
                        {
                            ctx.PlayerParty.IsMoving = false;
                        }
                        _UpdateTopInfoStatus(isWaiting ? "正在扎营等待..." : "");
                    }
                }
                break;
            case "skill_tree":
                _CloseAllPanels();
                _OpenSkillTree();
                break;
            case "territory":
                _CloseAllPanels();
                _OpenTerritoryUI();
                break;
            case "settings":
                _OpenSettings();
                break;
            default:
                EmitSignal(SignalName.MenuOpened, actionName);
                break;
        }
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>
    /// 更新顶部信息栏（含新增的速度/士气/声望）
    /// </summary>
    public void UpdateTopInfo(int year, int month, int day, string season, string clock,
        int gold, int food, int foodMax, string speedStatus, string moraleStatus, int reputation)
    {
        _dayLabel.Text = $"\U0001f4c5 {year}\u5e74 {month}\u6708 {day}\u65e5";
        _goldLabel.Text = $"\U0001f4b0 \u91d1\u5e01:{gold}";
        _foodLabel.Text = $"\U0001f356 {food}/{foodMax}";
        _speedStatusLabel.Text = $"\u2694\ufe0f {speedStatus}";
        _moraleStatusLabel.Text = $"\U0001f3ad {moraleStatus}";
        _reputationLabel.Text = $"\U0001f91d \u58f0\u671b:{reputation}";
        _speedLabel.Text = $"\U0001f324 {season}";
        _moraleLabel.Text = $"\u23f3 {clock}";
    }

    /// <summary>
    /// 向后兼容的旧签名（不含速度/士气/声望参数）
    /// </summary>
    public void UpdateTopInfo(int year, int month, int day, string season, string clock, int gold, int food, int foodMax)
    {
        UpdateTopInfo(year, month, day, season, clock, gold, food, foodMax, "\u6b63\u5e38", "\u6b63\u5e38", 0);
    }

    /// <summary>更新右上角地形显示</summary>
    public void UpdateTerrainDisplay(string terrainName, Color terrainColor)
    {
        _terrainLabel.Text = $"\U0001f30d {terrainName}";
        _terrainLabel.AddThemeColorOverride("font_color", terrainColor);
    }

    /// <summary>更新天气显示</summary>
    public void UpdateWeatherDisplay(string weatherText)
    {
        _weatherLabel.Text = weatherText;
    }

    /// <summary>
    /// 快捷键入口 — 由 OverworldScene 调用，触发对应面板操作。
    /// 如果目标面板已打开则关闭（toggle 行为）。
    /// </summary>
    public void HandleHotkey(string action)
    {
        // 检查目标面板是否已打开 → toggle 关闭
        bool targetOpen = action switch
        {
            "army" or "inventory" or "party" => _partyPanel != null && _partyPanel.Visible,
            "character" => _characterDetail != null && _characterDetail.Get("visible").AsBool(),
            "skill_tree" => _skillTreeUi != null && _skillTreeUi.Get("visible").AsBool(),
            "quests" => _questLog is Control qlCtrl && qlCtrl.Visible,
            "territory" => false,
            _ => false,
        };

        if (targetOpen)
        {
            _CloseAllPanels();
            return;
        }

        _OnButtonPressed(action);
    }

    /// <summary>
    /// 对接 C# OverworldScene.UpdateUIInfo 的统一入口
    /// </summary>
    public void UpdateInfo(GodotObject playerUnitData, GodotObject economy)
    {
        if (economy == null) return;

        int year = (int)economy.Get("Year").AsInt32();
        int month = (int)economy.Get("Month").AsInt32();
        int day = (int)economy.Get("DaysPassed").AsInt32();
        int gold = (int)economy.Get("Gold").AsInt32();
        int food = (int)economy.Get("Food").AsInt32();
        int foodMax = (int)economy.Get("FoodMax").AsInt32();

        string season = "";
        if (economy.HasMethod("GetSeasonName"))
            season = economy.Call("GetSeasonName").AsString();

        string clock = $"{economy.Get("CurrentHour").AsInt32():D2}:00";

        // Try to get new fields from economy; use defaults if not available
        string speedStatus = "\u6b63\u5e38";
        string moraleStatus = "\u6b63\u5e38";
        int reputation = 0;

        try
        {
            var speedVar = economy.Get("SpeedStatus");
            if (speedVar.VariantType == Variant.Type.String)
                speedStatus = speedVar.AsString();
        }
        catch { /* property not available yet */ }

        try
        {
            var moraleVar = economy.Get("MoraleStatus");
            if (moraleVar.VariantType == Variant.Type.String)
                moraleStatus = moraleVar.AsString();
        }
        catch { /* property not available yet */ }

        try
        {
            var repVar = economy.Get("Reputation");
            if (repVar.VariantType != Variant.Type.Nil)
                reputation = repVar.AsInt32();
        }
        catch { /* property not available yet */ }

        UpdateTopInfo(year, month, day, season, clock, gold, food, foodMax, speedStatus, moraleStatus, reputation);
    }

    /// <summary>
    /// 打开城镇界面
    /// </summary>
    public void OpenTown(string townName, Godot.Collections.Dictionary? townData = null)
    {
        _CloseAllPanels();
        _townUi.OpenTown(townName, townData ?? new Godot.Collections.Dictionary());
    }

    /// <summary>
    /// 更新顶部状态文字（扎营等）
    /// </summary>
    private void _UpdateTopInfoStatus(string status)
    {
        if (!string.IsNullOrEmpty(status))
        {
            _speedStatusLabel.Text = $"\u2694\ufe0f {status}";
            _speedStatusLabel.AddThemeColorOverride("font_color", Colors.Yellow);
        }
        else
        {
            _speedStatusLabel.Text = "\u2694\ufe0f \u6b63\u5e38";
            _speedStatusLabel.RemoveThemeColorOverride("font_color");
        }
    }

    private void _OpenCharacterDetail()
    {
        if (_characterDetail == null)
        {
            _characterDetail = new BladeHex.UI.CharacterDetailPanel();
            if (_characterDetail is Control ctrl)
            {
                ctrl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _root.AddChild(ctrl);
            }
        }
        if (_characterDetail is Control c) c.Visible = true;
    }

    private void _OpenArmyManagement()
    {
        _OpenPartyPanel();
    }

    private void _OpenPartyPanel()
    {
        // 从 OverworldScene 的 PlayerParty 获取 roster 和 inventory
        var ctx = GetContext();
        PartyRoster? roster = null;
        PartyInventory? inventory = null;

        if (ctx?.PlayerParty != null)
        {
            roster = ctx.PlayerParty.Roster;
            inventory = ctx.PlayerParty.Inventory;
        }

        if (roster != null && inventory != null)
            _partyPanel.Open(roster, inventory);
        else
            _partyPanel.OpenTab("party");
    }

    private void _OpenTerritoryUI()
    {
        // TerritoryUI exists at BladeHex.View.UI.Overworld.TerritoryUI
        GD.Print("[OverworldUI] \u9886\u5730\u7ba1\u7406\u9762\u677f \u2014 \u5f85\u5b8c\u5584");
    }

    private void _OpenQuestLog()
    {
        if (_questLog == null)
        {
            _questLog = new BladeHex.UI.QuestLog();
            if (_questLog is Control questCtrl)
            {
                questCtrl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _root.AddChild(questCtrl);
            }
        }

        if (_questLog is Control qc)
            qc.Visible = true;
        else if (_questLog.HasMethod("show_log"))
            _questLog.Call("show_log");
    }

    private void _OpenSkillTree()
    {
        if (_skillTreeUi == null)
        {
            _skillTreeUi = new BladeHex.UI.SkillTreeUI();
            if (_skillTreeUi is Control skillCtrl)
            {
                skillCtrl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                _root.AddChild(skillCtrl);
            }
        }

        // 获取 SkillTreeManager 单例和当前角色的技能盘
        var stm = SkillTreeManager.GetInstance();
        if (stm == null || stm.TreeData == null)
        {
            GD.PrintErr("[OverworldUI] SkillTreeManager 未初始化");
            if (_skillTreeUi is Control sc2) sc2.Visible = true;
            return;
        }

        // 从 OverworldScene 获取队长数据
        CharacterSkillTree? charTree = null;
        var ctx = GetContext();
        if (ctx?.PlayerParty?.Roster != null && ctx.PlayerParty.Roster.Count > 0)
        {
            var leader = ctx.PlayerParty.Roster.Members[0];
            long charId = (long)leader.GetInstanceId();

            // 获取或创建角色技能盘
            charTree = stm.GetSkillTree(charId);
            if (charTree == null)
            {
                charTree = stm.CreateSkillTree(charId, leader.Level);
                stm.InitCharacterLevel(charId, leader.Level);
            }
        }

        // 如果没有队伍数据，创建一个临时技能盘供浏览
        if (charTree == null)
        {
            charTree = new CharacterSkillTree(stm.TreeData, 1);
        }

        // 调用 UI 的 OpenSkillTree 传入数据
        if (_skillTreeUi is BladeHex.UI.SkillTreeUI skillTreeUi)
        {
            skillTreeUi.OpenSkillTree(charTree, stm.TreeData);
        }
        else if (_skillTreeUi is Control sc)
        {
            sc.Visible = true;
        }
    }

    private void _OpenSettings()
    {
        if (_settingsPanel == null)
        {
            _settingsPanel = _BuildSettingsPanel();
            _root.AddChild((Control)_settingsPanel);
        }

        if (_settingsPanel is Control settingsCtrl)
            settingsCtrl.Visible = true;
    }

    private Control _BuildSettingsPanel()
    {
        var panel = new PanelContainer();
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var bg = new StyleBoxFlat { BgColor = new Color(0.0f, 0.0f, 0.0f, 0.6f) };
        bg.SetBorderWidthAll(0);
        panel.AddThemeStyleboxOverride("panel", bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(center);

        var inner = new PanelContainer();
        var innerBg = MakePanelStyle(BgPrimary, BorderHighlight, 2, RadiusMd, 30);
        inner.AddThemeStyleboxOverride("panel", innerBg);
        center.AddChild(inner);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", SpacingLg);
        vbox.CustomMinimumSize = new Vector2(380, 0);
        inner.AddChild(vbox);

        vbox.AddChild(_CreateTitleLabel("\u8bbe \u7f6e"));

        // 音量
        vbox.AddChild(_CreateBodyLabel("\u4e3b\u97f3\u91cf"));
        var volumeSlider = new HSlider();
        volumeSlider.MinValue = 0;
        volumeSlider.MaxValue = 100;
        volumeSlider.Value = 80;
        volumeSlider.CustomMinimumSize = new Vector2(300, 30);
        volumeSlider.ValueChanged += (val) =>
        {
            float db = val > 0 ? Mathf.LinearToDb((float)val / 100.0f) : -80.0f;
            AudioServer.SetBusVolumeDb(0, db);
        };
        vbox.AddChild(volumeSlider);

        // 全屏
        var fullscreenCheck = new CheckBox { Text = "\u5168\u5c4f\u6a21\u5f0f", ButtonPressed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen };
        fullscreenCheck.AddThemeFontSizeOverride("font_size", FontSizeMd);
        fullscreenCheck.Toggled += (pressed) =>
        {
            DisplayServer.WindowSetMode(pressed ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        };
        vbox.AddChild(fullscreenCheck);

        // 关闭
        var closeBtn = _CreateButton("\u8fd4 \u56de", new Vector2(150, ButtonHeightLg));
        closeBtn.Pressed += () => { panel.Visible = false; };
        vbox.AddChild(closeBtn);

        return panel;
    }

    private void _CloseAllPanels()
    {
        if (_partyPanel != null) _partyPanel.Visible = false;
        if (_characterDetail != null) _characterDetail.Set("visible", false);
        if (_skillTreeUi != null) _skillTreeUi.Set("visible", false);
        if (_questLog != null && _questLog is Control qlCtrl) qlCtrl.Visible = false;
        if (_townUi != null) _townUi.Visible = false;
    }

    // ============================================================================
    // 输入处理
    // ============================================================================

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
        {
            // 全局菜单已开 → 让它自己处理
            var gameMenu = GetNodeOrNull<BladeHex.UI.Global.GameMenuManager>("/root/GameMenuManager");
            if (gameMenu != null && gameMenu.IsOpen)
                return;

            // 设置面板（旧版兼容）
            if (_settingsPanel != null && _settingsPanel.Get("visible").AsBool())
            {
                if (_settingsPanel is Control settingsCtrl)
                    settingsCtrl.Visible = false;
                else if (_settingsPanel.HasMethod("hide_settings"))
                    _settingsPanel.Call("hide_settings");
                GetViewport().SetInputAsHandled();
                return;
            }

            bool anyPanelOpen = (_partyPanel != null && _partyPanel.Visible) ||
                (_characterDetail != null && _characterDetail.Get("visible").AsBool()) ||
                (_skillTreeUi != null && _skillTreeUi.Get("visible").AsBool()) ||
                (_questLog != null && _questLog is Control qlc && qlc.Visible) ||
                (_townUi != null && _townUi.Visible);

            if (anyPanelOpen)
                _CloseAllPanels();
            else
            {
                // 打开全局系统菜单
                gameMenu?.Toggle();
            }

            GetViewport().SetInputAsHandled();
        }
    }

    // ============================================================================
    // UI 组件工厂 — 委托给 UIFactory 统一实现
    // ============================================================================

    private static StyleBoxFlat MakePanelStyle(Color bg, Color border, int borderWidth = 1, int radius = 8, int margin = 8)
    {
        var s = new StyleBoxFlat { BgColor = bg };
        s.SetBorderWidthAll(borderWidth);
        s.BorderColor = border;
        s.SetCornerRadiusAll(radius);
        s.SetContentMarginAll(margin);
        return s;
    }

    private Button _CreateButton(string text, Vector2 minSize)
        => _factory.CreateButton(text, minSize);

    private Label _CreateTitleLabel(string text, int fontSize = FontSizeXl)
        => _factory.CreateTitleLabel(text, fontSize);

    private Label _CreateBodyLabel(string text, Color? color = null)
        => _factory.CreateBodyLabel(text, color);

    private Label _CreateMutedLabel(string text)
        => _factory.CreateMutedLabel(text);

    private RichTextLabel _CreateRichText(Vector2 minSize)
        => _factory.CreateRichText(minSize);

    private HSeparator _CreateSeparatorH()
        => _factory.CreateSeparatorH();

    private VSeparator _CreateSeparatorV()
        => _factory.CreateSeparatorV();
}
