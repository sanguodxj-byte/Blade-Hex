using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Localization;
using BladeHex.Scenes.Overworld;
using BladeHex.Scenes.Overworld2d;
using BladeHex.Strategic;
using BladeHex.Strategic.WorldEvents;
using BladeHex.UI;
using BladeHex.View.AssetSystem;
using Godot;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class OverworldUI : CanvasLayer
{
    private const string SkillTreeAction = "skill_tree";
    private const string SkillTreeIconId = "Astral_Node_Active";
    private const string SkillTreeIconPath = "res://BladeHexFrontend/src/assets/ui/Astral_Node_Active.png";
    private const string IconButtonShaderPath = "res://BladeHexFrontend/src/assets/shaders/icon_button.gdshader";

    [Signal] public delegate void MenuOpenedEventHandler(string menuName);
    [Signal] public delegate void PartyClickedEventHandler();
    [Signal] public delegate void InventoryClickedEventHandler();
    [Signal] public delegate void PanelDismissedEventHandler();

    private Control _root = null!;
    private OverworldTopHUD _topHUD = null!;
    private OverworldBottomBar _bottomBar = null!;
    private OverworldDayNightClock _clock = null!;
    private OverworldPanelManager _panelManager = null!;
    private PanelContainer _escMenu = null!;
    private SimpleFloatingTooltip _skillTreeTooltip = null!;

    private int _unreadNewsCount;

    public EconomyManager EconomyManager { get; set; } = null!;
    public OverworldEntityManager? EntityMgr { get; set; }
    public SaveManager SaveMgr { get; private set; } = new();

    private IOverworldContext? _context;
    private UIFactory _factory = null!;

    public Control TopPanel => _topHUD;
    public Control BottomPanel => _bottomBar;

    public override void _Ready()
    {
        Layer = 10;
        _factory = new UIFactory();
        SetupUi();
        ConnectGlobalMenuSignals();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Keycode != Key.Escape)
            return;

        var gameMenu = Globals.GameMenuOrNull;
        if (gameMenu != null && gameMenu.IsOpen)
            return;

        if (AnyManagedPanelVisible())
            _panelManager.CloseAllPanels();
        else
            gameMenu?.Toggle();

        GetViewport().SetInputAsHandled();
    }

    public void UpdateTopInfo(int year, int month, int day, string season, string clock,
        int gold, int food, int foodMax, string speedStatus, int reputation)
    {
        _clock.SetTime(clock);
        _clock.UpdateSeason(season);
        _clock.UpdateDate($"{year}-{month:D2}-{day:D2}");
        _topHUD.UpdateTopInfo(year, month, day, season, clock, gold, food, foodMax, speedStatus, reputation);
    }

    public void UpdateTopInfo(int year, int month, int day, string season, string clock, int gold, int food, int foodMax)
    {
        UpdateTopInfo(year, month, day, season, clock, gold, food, foodMax, "Normal", 0);
    }

    public void UpdateTerrainDisplay(string terrainName, Color terrainColor)
    {
        _topHUD.UpdateTerrainDisplay(terrainName, terrainColor);
    }

    public void UpdateWeatherDisplay(string weatherText)
    {
        _clock.UpdateWeather(weatherText);
        _topHUD.UpdateWeatherDisplay(weatherText);
    }

    public void HandleHotkey(string action)
    {
        if (_panelManager.IsPanelVisible(action))
        {
            _panelManager.CloseAllPanels();
            return;
        }

        OnButtonPressed(action);
    }

    public void UpdateInfo(GodotObject playerUnitData, GodotObject economy)
    {
        if (economy == null)
            return;

        var year = (int)economy.Get("Year").AsInt32();
        var month = (int)economy.Get("Month").AsInt32();
        var day = (int)economy.Get("DaysPassed").AsInt32();
        var gold = (int)economy.Get("Gold").AsInt32();
        var food = (int)economy.Get("Food").AsInt32();
        var foodMax = (int)economy.Get("FoodMax").AsInt32();
        var season = economy.HasMethod("GetSeasonName") ? economy.Call("GetSeasonName").AsString() : "";
        var clock = $"{economy.Get("CurrentHour").AsInt32():D2}:00";
        var speedStatus = ReadStringProperty(economy, "SpeedStatus", "Normal");
        var reputation = ReadIntProperty(economy, "Reputation", 0);

        UpdateTopInfo(year, month, day, season, clock, gold, food, foodMax, speedStatus, reputation);
        SyncWaitingStatus();
    }

    public void OpenPartyShop(string shopName, EconomyManager economy, List<ItemData> stock, int prosperity = 50,
        OverworldPOI? poi = null, ReputationTracker? reputation = null, WorldEventEngine? worldEngine = null,
        IOverworldContext? overworldScene = null)
    {
        _panelManager.OpenPartyShop(shopName, economy, stock, prosperity, poi, reputation, worldEngine, overworldScene);
    }

    public void OpenPartyLoot(List<ItemData> loot, int goldGranted = 0, int xpGranted = 0)
    {
        _panelManager.OpenPartyLoot(loot, goldGranted, xpGranted);
    }

    public void UpdatePlayerSpeed(float speedValue, bool isCamping, string? speedTooltip = null)
    {
        _topHUD.UpdatePlayerSpeed(speedValue, isCamping, speedTooltip);
    }

    public void OnNewsReceived()
    {
        if (_panelManager.NewsPanel != null && _panelManager.NewsPanel.Visible)
            return;

        _unreadNewsCount++;
        _bottomBar.UpdateNewsButtonText(_unreadNewsCount);
    }

    public IOverworldContext? GetContextInternal()
    {
        return GetContext();
    }

    private void SetupUi()
    {
        _root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        SetupClock();
        SetupTopHud();
        SetupBottomBar();
        SetupMiddleSkillTreeBar();

        _panelManager = new OverworldPanelManager(_root, this);
        SetupEscMenu();
    }

    private void SetupClock()
    {
        _clock = new OverworldDayNightClock { Name = "DayNightClock" };
        _root.AddChild(_clock);
        _clock.Initialize();
    }

    private void SetupTopHud()
    {
        _topHUD = new OverworldTopHUD { Name = "TopHUD" };
        _root.AddChild(_topHUD);
        _topHUD.Initialize();
    }

    private void SetupBottomBar()
    {
        _bottomBar = new OverworldBottomBar { Name = "BottomBar" };
        _bottomBar.ButtonPressed += OnButtonPressed;
        _root.AddChild(_bottomBar);
        _bottomBar.Initialize();
    }

    private void SetupEscMenu()
    {
        _escMenu = new PanelContainer
        {
            Visible = false,
        };
        _escMenu.AddThemeStyleboxOverride("panel", MakeEscBackdropStyle());
        _escMenu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_escMenu);

        var escCenter = new CenterContainer();
        escCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _escMenu.AddChild(escCenter);

        var escInner = new PanelContainer();
        escInner.AddThemeStyleboxOverride("panel", MakeEscPanelStyle());
        escCenter.AddChild(escInner);

        var escVbox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(220, 0),
        };
        escVbox.AddThemeConstantOverride("alignment", 1);
        escVbox.AddThemeConstantOverride("separation", 12);
        escInner.AddChild(escVbox);

        var escTitle = new Label
        {
            Text = L10n.Tr("MENU_SYSTEM_TITLE"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        escTitle.AddThemeFontSizeOverride("font_size", 20);
        escTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));
        escVbox.AddChild(escTitle);

        CreateEscButton(L10n.Tr("MENU_SAVE_GAME"), "save", escVbox);
        CreateEscButton(L10n.Tr("MENU_LOAD_GAME"), "load", escVbox);
        CreateEscButton(L10n.Tr("MENU_SETTINGS"), "settings", escVbox);
        CreateEscButton(L10n.Tr("MENU_RESUME_GAME"), "resume", escVbox);
        CreateMainMenuButton(escVbox);
        CreateExitButton(escVbox);
    }

    private void CreateEscButton(string text, string actionName, Control parent)
    {
        var button = _factory.CreateButton(text, new Vector2(200, 45));
        button.Pressed += () => OnButtonPressed(actionName);
        parent.AddChild(button);
    }

    private void CreateMainMenuButton(Control parent)
    {
        var button = _factory.CreateButton(L10n.Tr("MENU_MAIN_MENU"), new Vector2(200, 45));
        button.Pressed += () =>
        {
            _escMenu.Visible = false;
            BladeHex.View.SceneTransition.ChangeSceneTo(GetTree(), "res://BladeHexFrontend/src/ui/main_menu/main_menu.tscn");
        };
        parent.AddChild(button);
    }

    private void CreateExitButton(Control parent)
    {
        var button = _factory.CreateButton(L10n.Tr("MENU_QUIT_GAME"), new Vector2(200, 45));
        button.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.25f));
        button.Pressed += () => GetTree().Quit();
        parent.AddChild(button);
    }

    private void SetupMiddleSkillTreeBar()
    {
        var middleBar = new PanelContainer
        {
            Name = "MiddleBar",
            CustomMinimumSize = new Vector2(96, 96),
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Begin,
            OffsetBottom = 0,
            OffsetTop = -96,
            OffsetLeft = -48,
            OffsetRight = 48,
        };
        middleBar.AddThemeStyleboxOverride("panel", MakeQuickBarStyle());
        middleBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);

        var button = CreateSkillTreeButton();
        middleBar.AddChild(button);
        _root.AddChild(middleBar);

        _skillTreeTooltip = new SimpleFloatingTooltip { Name = "SkillTreeTooltip" };
        _root.AddChild(_skillTreeTooltip);
    }

    private Button CreateSkillTreeButton()
    {
        var button = _factory.CreateButton("", new Vector2(80, 80));
        button.FocusMode = Control.FocusModeEnum.None;
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        var icon = TextureAssetResolver.LoadUiTexture(SkillTreeIconId, SkillTreeIconPath);
        if (icon != null)
        {
            button.Icon = icon;
            button.ExpandIcon = true;
            button.IconAlignment = HorizontalAlignment.Center;
            button.VerticalIconAlignment = VerticalAlignment.Center;
        }
        else
        {
            GD.PushWarning($"[OverworldUI] Missing skill tree icon: {SkillTreeIconId}");
        }

        ApplyTransparentButtonStyle(button);
        ApplyIconShader(button);
        WireSkillTreeButton(button);
        return button;
    }

    private void WireSkillTreeButton(Button button)
    {
        button.MouseEntered += () =>
        {
            SetShaderFlag(button, "is_hovered", true);
            _skillTreeTooltip.SetText(L10n.Tr("TOOLTIP_SKILL_TREE"));
            _skillTreeTooltip.ShowAtMouse();
        };
        button.MouseExited += () =>
        {
            SetShaderFlag(button, "is_hovered", false);
            SetShaderFlag(button, "is_pressed", false);
            _skillTreeTooltip.HidePanel();
        };
        button.ButtonDown += () => SetShaderFlag(button, "is_pressed", true);
        button.ButtonUp += () => SetShaderFlag(button, "is_pressed", false);
        button.Pressed += () => OnButtonPressed(SkillTreeAction);
    }

    private void OnButtonPressed(string actionName)
    {
        _escMenu.Visible = false;

        switch (actionName)
        {
            case "resume":
                break;
            case "save":
                SaveCurrentGame();
                break;
            case "load":
                EmitSignal(SignalName.MenuOpened, "load_game");
                break;
            case "party":
            case "inventory":
            case "army":
                _panelManager.CloseAllPanels();
                _panelManager.OpenPartyPanel();
                break;
            case "quests":
                _panelManager.CloseAllPanels();
                _panelManager.OpenQuestLog();
                break;
            case "camp":
                ToggleCamping();
                break;
            case SkillTreeAction:
                _panelManager.CloseAllPanels();
                _panelManager.OpenSkillTree();
                break;
            case "territory":
                _panelManager.CloseAllPanels();
                GD.Print("[OverworldUI] Territory panel is not implemented yet.");
                break;
            case "economy_panel":
                _panelManager.CloseAllPanels();
                _panelManager.ToggleEconomyPanel();
                break;
            case "kingdom_panel":
                _panelManager.CloseAllPanels();
                _panelManager.ToggleKingdomPanel();
                break;
            case "news_panel":
                _panelManager.CloseAllPanels();
                _panelManager.ToggleNewsPanel();
                break;
            case "encyclopedia_panel":
                _panelManager.CloseAllPanels();
                _panelManager.ToggleEncyclopediaPanel();
                break;
            case "settings":
                OpenSettings();
                break;
            default:
                EmitSignal(SignalName.MenuOpened, actionName);
                break;
        }
    }

    private void SaveCurrentGame()
    {
        if (EconomyManager is not EconomyManager economy)
            return;

        var context = GetContext();
        var playerUnit = context?.PlayerUnitData;
        if (playerUnit == null)
        {
            context?.SaveWorldData();
            return;
        }

        var playerPosition = context?.PlayerParty?.Position ?? Vector2.Zero;
        var playerRaceId = context?.PlayerRaceId ?? 0;
        var entityManager = (context as OverworldScene2D)?.EntityMgr;
        var gameState = Globals.StateOrNull;
        var saveData = SaveManager.BuildSaveData(
            playerUnit,
            playerRaceId,
            playerPosition,
            economy,
            entityManager,
            gameState?.WorldGen.Seed ?? 0,
            gameState?.WorldGen.Size ?? 1,
            gameState?.Save.CurrentSaveId);

        SaveMgr.SaveGame(saveData, gameState?.Save.CurrentSaveId);
        context?.SaveWorldData();
    }

    private void ToggleCamping()
    {
        var context = GetContext();
        if (context == null)
            return;

        context.IsWaiting = !context.IsWaiting;
        if (context.IsWaiting && context.PlayerParty != null)
            context.PlayerParty.IsMoving = false;

        _topHUD.UpdateTopInfoStatus(context.IsWaiting ? L10n.Tr("STATUS_WAITING_IN_CAMP") : "");
    }

    private void SyncWaitingStatus()
    {
        var context = GetContext();
        if (context == null)
            return;

        _topHUD.UpdateTopInfoStatus(context.IsWaiting ? L10n.Tr("STATUS_WAITING_IN_CAMP") : "");
    }

    private IOverworldContext? GetContext()
    {
        if (_context != null)
            return _context;

        _context = GetParent() as IOverworldContext;
        return _context;
    }

    private void OpenSettings()
    {
        Globals.GameMenuOrNull?.OpenSettings();
    }

    private void ConnectGlobalMenuSignals()
    {
        var gameMenu = Globals.GameMenuOrNull;
        if (gameMenu == null)
            return;

        gameMenu.SaveRequested += () => OnButtonPressed("save");
        gameMenu.LoadRequested += () => OnButtonPressed("load");
    }

    private bool AnyManagedPanelVisible()
    {
        return _panelManager.IsPanelVisible("party")
            || _panelManager.IsPanelVisible(SkillTreeAction)
            || _panelManager.IsPanelVisible("quests")
            || _panelManager.IsPanelVisible("economy_panel")
            || _panelManager.IsPanelVisible("kingdom_panel")
            || _panelManager.IsPanelVisible("news_panel")
            || _panelManager.IsPanelVisible("encyclopedia_panel");
    }

    private static string ReadStringProperty(GodotObject source, string propertyName, string fallback)
    {
        try
        {
            var value = source.Get(propertyName);
            return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static int ReadIntProperty(GodotObject source, string propertyName, int fallback)
    {
        try
        {
            var value = source.Get(propertyName);
            return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
        }
        catch
        {
            return fallback;
        }
    }

    private static void ApplyTransparentButtonStyle(Button button)
    {
        var emptyStyle = new StyleBoxEmpty();
        button.AddThemeStyleboxOverride("normal", emptyStyle);
        button.AddThemeStyleboxOverride("hover", emptyStyle);
        button.AddThemeStyleboxOverride("pressed", emptyStyle);
        button.AddThemeStyleboxOverride("disabled", emptyStyle);
        button.AddThemeStyleboxOverride("focus", emptyStyle);
    }

    private static void ApplyIconShader(Button button)
    {
        var shader = ShaderAssetResolver.Load("icon_button", IconButtonShaderPath);
        if (shader == null)
            return;

        button.Material = new ShaderMaterial
        {
            Shader = shader,
        };
    }

    private static void SetShaderFlag(Button button, string name, bool value)
    {
        (button.Material as ShaderMaterial)?.SetShaderParameter(name, value);
    }

    private static StyleBoxFlat MakeEscBackdropStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.0f, 0.0f, 0.0f, 0.6f),
            BorderColor = new Color(0.5f, 0.45f, 0.3f, 0.8f),
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(8);
        return style;
    }

    private static StyleBoxFlat MakeEscPanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.10f, 0.85f),
            BorderColor = new Color(0.5f, 0.45f, 0.3f, 0.8f),
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(8);
        style.SetContentMarginAll(30);
        return style;
    }

    private static StyleBoxFlat MakeQuickBarStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.10f, 0.76f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.72f, 0.58f, 0.35f, 0.65f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8f,
            ContentMarginRight = 8f,
            ContentMarginTop = 8f,
            ContentMarginBottom = 8f,
        };
    }
}
