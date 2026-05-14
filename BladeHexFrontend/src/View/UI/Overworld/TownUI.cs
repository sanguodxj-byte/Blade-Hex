// TownUI.cs
// Town UI system - Includes tavern (recruit), shop (trade), rest (recovery) sub-panels
// Corresponds to design doc 04-StrategicLayer.md - Town UI
// Corresponds to design doc 12-RacesAndRecruitment.md - Recruitment system
// Corresponds to design doc 06-EquipmentAndItems.md - Shop
using Godot;
using Godot.Collections;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class TownUI : PanelContainer
{
    // ============================================================================
    // 淇″彿
    // ============================================================================
    [Signal]
    public delegate void CloseRequestedEventHandler();

    [Signal]
    public delegate void RecruitClickedEventHandler(Dictionary heroData);

    [Signal]
    public delegate void BuyClickedEventHandler(string itemId, int quantity);

    [Signal]
    public delegate void SellClickedEventHandler(string itemId, int quantity);

    [Signal]
    public delegate void RestClickedEventHandler(string type);

    // ============================================================================
    // Tab鏋氫妇
    // ============================================================================
    public enum TownTab
    {
        Tavern,
        Shop,
        Rest,
    }

    // ============================================================================
    // 涓婚甯搁噺
    // ============================================================================
    private static readonly Color BgPrimary = new(0.08f, 0.08f, 0.10f, 0.85f);
    private static readonly Color BgSecondary = new(0.12f, 0.12f, 0.14f, 0.80f);
    private static readonly Color BgCard = new(0.15f, 0.14f, 0.18f, 0.75f);
    private static readonly Color BgCardHover = new(0.25f, 0.22f, 0.30f, 0.90f);
    private static readonly Color BorderDefault = new(0.3f, 0.3f, 0.35f, 0.6f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color BorderFriendly = new(0.2f, 0.5f, 0.8f, 0.8f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPositive = new(0.3f, 0.85f, 0.3f);
    private static readonly Color TextNegative = new(0.9f, 0.3f, 0.25f);

    private const int FontSizeXxl = 28;
    private const int FontSizeXl = 22;
    private const int FontSizeLg = 18;
    private const int FontSizeMd = 16;
    private const int FontSizeSm = 14;
    private const int FontSizeXs = 12;
    private const int SpacingMd = 10;
    private const int SpacingSm = 6;
    private const int SpacingLg = 14;
    private const int SpacingXl = 20;
    private const int SpacingXxl = 28;
    private const int RadiusMd = 8;
    private const int RadiusSm = 4;
    private const int RadiusLg = 12;
    private const int ButtonHeight = 42;
    private const int ButtonHeightLg = 50;

    // ============================================================================
    // 瀛楁
    // ============================================================================
    private System.Collections.Generic.Dictionary<TownTab, Button> _tabButtons = new System.Collections.Generic.Dictionary<TownTab, Button>();  // TownTab 鈫?Button
    private PanelContainer _tabContainer = null!;
    private VBoxContainer _contentArea = null!;
    private TownTab _currentTab = TownTab.Tavern;
    private Label _townNameLabel = null!;

    // 閰掗缁勪欢
    private VBoxContainer _tavernList = null!;
    private RichTextLabel _tavernDetail = null!;
    private Button _recruitBtn = null!;

    // 鍟嗗簵缁勪欢
    private GridContainer _shopGrid = null!;
    private RichTextLabel _shopDetail = null!;
    private Button _buyBtn = null!;
    private GridContainer _sellGrid = null!;

    // 浼戞伅缁勪欢
    private Button _shortRestBtn = null!;
    private Button _longRestBtn = null!;

    // 鍩庨晣鏁版嵁
    private Dictionary _townData = new();
    private Label _goldLabel = null!;

    // ============================================================================
    // 鐢熷懡鍛ㄦ湡
    // ============================================================================

    public override void _Ready()
    {
        _Setup();
        Visible = false;
    }

    // ============================================================================
    // UI鏋勫缓
    // ============================================================================

    private void _Setup()
    {
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddThemeStyleboxOverride("panel", MakePanelStyle(BgPrimary, BorderHighlight, 3, RadiusLg, 0));

        var rootMargin = new MarginContainer();
        rootMargin.AddThemeConstantOverride("margin_left", 40);
        rootMargin.AddThemeConstantOverride("margin_right", 40);
        rootMargin.AddThemeConstantOverride("margin_top", 30);
        rootMargin.AddThemeConstantOverride("margin_bottom", 30);
        AddChild(rootMargin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", SpacingMd);
        rootMargin.AddChild(mainVbox);

        // 鈹€鈹€鈹€ 椤堕儴 鈹€鈹€鈹€
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", SpacingMd);
        mainVbox.AddChild(header);

        _townNameLabel = CreateTitleLabel("\u57ce\u9547", FontSizeXxl);
        _townNameLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        header.AddChild(_townNameLabel);

        _goldLabel = CreateBodyLabel("\u91d1\u5e01: 0", TextAccent);
        header.AddChild(_goldLabel);

        var closeBtn = CreateButton("\u79bb\u5f00\u57ce\u9547 (ESC)", new Vector2(140, 36));
        closeBtn.Pressed += () => { Visible = false; EmitSignal(SignalName.CloseRequested); };
        header.AddChild(closeBtn);

        mainVbox.AddChild(CreateSeparator());

        // 鈹€鈹€鈹€ Tab鏍?鈹€鈹€鈹€
        var tabBar = new HBoxContainer();
        tabBar.AddThemeConstantOverride("separation", SpacingSm);
        mainVbox.AddChild(tabBar);

        _CreateTabButton(tabBar, TownTab.Tavern, "\u9152\u9986 (\u62db\u52df)");
        _CreateTabButton(tabBar, TownTab.Shop, "\u5546\u5e97 (\u4e70\u5356)");
        _CreateTabButton(tabBar, TownTab.Rest, "\u4f11\u606f (\u6062\u590d)");

        mainVbox.AddChild(CreateSeparator());

        // 鈹€鈹€鈹€ 鍐呭鍖?鈹€鈹€鈹€
        _tabContainer = new PanelContainer();
        _tabContainer.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        _tabContainer.AddThemeStyleboxOverride("panel", MakePanelStyle(BgSecondary, BorderDefault));
        mainVbox.AddChild(_tabContainer);

        var contentMargin = new MarginContainer();
        contentMargin.AddThemeConstantOverride("margin_left", 16);
        contentMargin.AddThemeConstantOverride("margin_right", 16);
        contentMargin.AddThemeConstantOverride("margin_top", 12);
        contentMargin.AddThemeConstantOverride("margin_bottom", 12);
        _tabContainer.AddChild(contentMargin);

        _contentArea = new VBoxContainer();
        _contentArea.AddThemeConstantOverride("separation", SpacingMd);
        contentMargin.AddChild(_contentArea);

        // 鍒濆鍖栨墍鏈夊瓙闈㈡澘鍐呭
        _SetupTavern();
        _SetupShop();
        _SetupRest();

        // 榛樿鏄剧ず閰掗
        _SwitchTab(TownTab.Tavern);
    }

    // ============================================================================
    // Tab
    // ============================================================================

    private void _CreateTabButton(Control parent, TownTab tab, string text)
    {
        var btn = CreateButton(text, new Vector2(0, 36));
        btn.Pressed += () => _SwitchTab(tab);
        parent.AddChild(btn);
        _tabButtons[tab] = btn;
    }

    private void _SwitchTab(TownTab tab)
    {
        _currentTab = tab;

        // 鏇存柊鎸夐挳楂樹寒
        foreach (var kvp in _tabButtons)
        {
            kvp.Value.Modulate = kvp.Key == tab ? new Color(1, 1, 1, 1) : new Color(0.6f, 0.6f, 0.6f, 0.7f);
        }

        // 娓呴櫎鍐呭
        foreach (Node child in _contentArea.GetChildren())
            if (child is Control ctrl) ctrl.Visible = false;

        // 鏄剧ず瀵瑰簲鍐呭
        switch (tab)
        {
            case TownTab.Tavern:
                _ShowTavern();
                break;
            case TownTab.Shop:
                _ShowShop();
                break;
            case TownTab.Rest:
                _ShowRest();
                break;
        }
    }

    // ============================================================================
    // 閰掗锛堟嫑鍕燂級
    // ============================================================================

    private void _SetupTavern()
    {
        var tavernHbox = new HBoxContainer();
        tavernHbox.AddThemeConstantOverride("separation", SpacingLg);
        tavernHbox.Name = "TavernContent";
        _contentArea.AddChild(tavernHbox);

        // 宸︿晶锛氬彲鎷涘嫙鑻遍泟鍒楄〃
        var leftPanel = new PanelContainer();
        leftPanel.CustomMinimumSize = new Vector2(280, 0);
        leftPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(BgCard, BorderDefault));
        tavernHbox.AddChild(leftPanel);

        var leftVbox = new VBoxContainer();
        leftVbox.AddThemeConstantOverride("separation", SpacingSm);
        leftPanel.AddChild(leftVbox);

        var title = CreateTitleLabel("\u53ef\u62db\u52df\u82f1\u96c4", FontSizeLg);
        leftVbox.AddChild(title);

        _tavernList = new VBoxContainer();
        _tavernList.AddThemeConstantOverride("separation", SpacingXs());
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        scroll.AddChild(_tavernList);
        leftVbox.AddChild(scroll);

        // 鍙充晶锛氳嫳闆勮鎯?
        var rightPanel = new PanelContainer();
        rightPanel.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        rightPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(BgCard, BorderDefault));
        tavernHbox.AddChild(rightPanel);

        var rightVbox = new VBoxContainer();
        rightVbox.AddThemeConstantOverride("separation", SpacingMd);
        rightPanel.AddChild(rightVbox);

        _tavernDetail = CreateRichText(new Vector2(0, 0));
        _tavernDetail.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        _tavernDetail.Text = "\u9009\u62e9\u4e00\u4f4d\u82f1\u96c4\u67e5\u770b\u8be6\u60c5";
        rightVbox.AddChild(_tavernDetail);

        _recruitBtn = CreateButton("\u62db\u52df (0\u91d1)", new Vector2(0, 40));
        _recruitBtn.Disabled = true;
        _recruitBtn.Pressed += _OnRecruitClicked;
        rightVbox.AddChild(_recruitBtn);

        tavernHbox.Visible = false;
    }

    private void _ShowTavern()
    {
        var tavern = _contentArea.GetNodeOrNull<Control>("TavernContent");
        if (tavern != null)
            tavern.Visible = true;
    }

    private void _OnRecruitClicked()
    {
        // Handled via external signal connection
    }

    // ============================================================================
    // Shop
    // ============================================================================

    private void _SetupShop()
    {
        var shopHbox = new HBoxContainer();
        shopHbox.AddThemeConstantOverride("separation", SpacingLg);
        shopHbox.Name = "ShopContent";
        _contentArea.AddChild(shopHbox);

        // 宸︿晶锛氬嚭鍞晢鍝?
        var leftPanel = new PanelContainer();
        leftPanel.CustomMinimumSize = new Vector2(320, 0);
        leftPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(BgCard, BorderDefault));
        shopHbox.AddChild(leftPanel);

        var leftVbox = new VBoxContainer();
        leftVbox.AddThemeConstantOverride("separation", SpacingSm);
        leftPanel.AddChild(leftVbox);

        var buyTitle = CreateTitleLabel("\u8d2d\u4e70", FontSizeLg);
        leftVbox.AddChild(buyTitle);

        _shopGrid = new GridContainer();
        _shopGrid.Columns = 5;
        _shopGrid.AddThemeConstantOverride("h_separation", SpacingSm);
        _shopGrid.AddThemeConstantOverride("v_separation", SpacingSm);
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        scroll.AddChild(_shopGrid);
        leftVbox.AddChild(scroll);

        // 鍙充晶锛氳鎯?鍑哄敭鑳屽寘
        var rightVbox = new VBoxContainer();
        rightVbox.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        rightVbox.AddThemeConstantOverride("separation", SpacingMd);
        shopHbox.AddChild(rightVbox);

        _shopDetail = CreateRichText(new Vector2(0, 0));
        _shopDetail.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        _shopDetail.Text = "\u9009\u62e9\u5546\u54c1\u67e5\u770b\u8be6\u60c5";
        rightVbox.AddChild(_shopDetail);

        _buyBtn = CreateButton("\u8d2d\u4e70", new Vector2(0, 36));
        _buyBtn.Disabled = true;
        rightVbox.AddChild(_buyBtn);

        rightVbox.AddChild(CreateSeparator());

        var sellTitle = CreateTitleLabel("\u51fa\u552e\u80cc\u5305\u7269\u54c1", FontSizeMd);
        rightVbox.AddChild(sellTitle);

        _sellGrid = new GridContainer();
        _sellGrid.Columns = 6;
        _sellGrid.AddThemeConstantOverride("h_separation", SpacingSm);
        _sellGrid.AddThemeConstantOverride("v_separation", SpacingSm);
        rightVbox.AddChild(_sellGrid);

        shopHbox.Visible = false;
    }

    private void _ShowShop()
    {
        var shop = _contentArea.GetNodeOrNull<Control>("ShopContent");
        if (shop != null)
            shop.Visible = true;
    }

    // ============================================================================
    // 浼戞伅
    // ============================================================================

    private void _SetupRest()
    {
        var restVbox = new VBoxContainer();
        restVbox.Name = "RestContent";
        restVbox.AddThemeConstantOverride("separation", SpacingXxl);
        _contentArea.AddChild(restVbox);

        var restTitle = CreateTitleLabel("\u8425\u5730\u4f11\u606f", FontSizeXxl);
        restTitle.HorizontalAlignment = HorizontalAlignment.Center;
        restVbox.AddChild(restTitle);

        // 鐭紤鎭?
        var shortPanel = new PanelContainer();
        shortPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(BgCard, BorderFriendly));
        restVbox.AddChild(shortPanel);

        var shortVbox = new VBoxContainer();
        shortVbox.AddThemeConstantOverride("separation", SpacingSm);
        shortPanel.AddChild(shortVbox);

        var shortTitle = CreateTitleLabel("\u77ed\u4f11\u606f", FontSizeLg);
        shortVbox.AddChild(shortTitle);

        var shortDesc = CreateBodyLabel("\u6062\u590d50%\u9b54\u529b\uff0c\u4e0d\u6062\u590dHP\u3002\u514d\u8d39\uff0c\u4f46\u4ec5\u9650\u6bcf\u5192\u96691\u6b21\u3002", TextSecondary);
        shortVbox.AddChild(shortDesc);

        _shortRestBtn = CreateButton("\u77ed\u4f11\u606f (\u514d\u8d39)", new Vector2(0, 40));
        _shortRestBtn.Pressed += () => EmitSignal(SignalName.RestClicked, "short");
        shortVbox.AddChild(_shortRestBtn);

        // 闀夸紤鎭?
        var longPanel = new PanelContainer();
        longPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(BgCard, BorderHighlight));
        restVbox.AddChild(longPanel);

        var longVbox = new VBoxContainer();
        longVbox.AddThemeConstantOverride("separation", SpacingSm);
        longPanel.AddChild(longVbox);

        var longTitle = CreateTitleLabel("\u957f\u4f11\u606f", FontSizeLg);
        longVbox.AddChild(longTitle);

        var longDesc = CreateBodyLabel("\u6062\u590d100% HP\u548c\u9b54\u529b\uff0c\u91cd\u7f6e\u6240\u6709\u51b7\u5374\u3002\u6d88\u80171\u5929\u8865\u7ed9\u3002", TextSecondary);
        longVbox.AddChild(longDesc);

        _longRestBtn = CreateButton("\u957f\u4f11\u606f (\u6d88\u8017\u8865\u7ed9)", new Vector2(0, 40));
        _longRestBtn.Pressed += () => EmitSignal(SignalName.RestClicked, "long");
        longVbox.AddChild(_longRestBtn);

        restVbox.Visible = false;
    }

    private void _ShowRest()
    {
        var rest = _contentArea.GetNodeOrNull<Control>("RestContent");
        if (rest != null)
            rest.Visible = true;
    }

    // ============================================================================
    // 鍏紑鎺ュ彛
    // ============================================================================

    /// <summary>
    /// 鎵撳紑鍩庨晣鐣岄潰
    /// </summary>
    public void OpenTown(string townName, Dictionary? townData = null)
    {
        _townData = townData ?? new Dictionary();
        _townNameLabel.Text = townName;
        Visible = true;
        _SwitchTab(TownTab.Tavern);
    }

    /// <summary>
    /// 鍏抽棴
    /// </summary>
    public void CloseTown()
    {
        Visible = false;
    }

    // ============================================================================
    // UI 缁勪欢宸ュ巶
    // ============================================================================

    private static int SpacingXs() => 2;

    private static StyleBoxFlat MakePanelStyle(Color bg, Color border, int borderWidth = 1, int radius = 8, int margin = 8)
    {
        var s = new StyleBoxFlat { BgColor = bg };
        s.SetBorderWidthAll(borderWidth);
        s.BorderColor = border;
        s.SetCornerRadiusAll(radius);
        s.SetContentMarginAll(margin);
        return s;
    }

    private static Button CreateButton(string text, Vector2 minSize)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = minSize;

        var normal = new StyleBoxFlat { BgColor = new Color(0.18f, 0.17f, 0.22f) };
        normal.SetBorderWidthAll(1);
        normal.BorderColor = new Color(0.3f, 0.3f, 0.35f, 0.6f);
        normal.SetCornerRadiusAll(8);
        normal.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat { BgColor = new Color(0.28f, 0.26f, 0.34f) };
        hover.SetBorderWidthAll(1);
        hover.BorderColor = new Color(0.5f, 0.45f, 0.3f, 0.8f);
        hover.SetCornerRadiusAll(8);
        hover.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("hover", hover);

        var pressed = new StyleBoxFlat { BgColor = new Color(0.12f, 0.11f, 0.15f) };
        pressed.SetBorderWidthAll(1);
        pressed.BorderColor = new Color(0.5f, 0.45f, 0.3f, 0.8f);
        pressed.SetCornerRadiusAll(8);
        pressed.SetContentMarginAll(4);
        btn.AddThemeStyleboxOverride("pressed", pressed);

        btn.AddThemeColorOverride("font_color", new Color(0.95f, 0.93f, 0.88f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.9f, 0.8f, 0.5f));
        btn.AddThemeColorOverride("font_pressed_color", TextSecondary);

        return btn;
    }

    private static Label CreateTitleLabel(string text, int fontSize = FontSizeXl)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", TextAccent);
        return lbl;
    }

    private static Label CreateBodyLabel(string text, Color? color = null)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", FontSizeMd);
        lbl.AddThemeColorOverride("font_color", color ?? TextPrimary);
        return lbl;
    }

    private static RichTextLabel CreateRichText(Vector2 minSize)
    {
        var rt = new RichTextLabel();
        rt.CustomMinimumSize = minSize;
        rt.BbcodeEnabled = true;
        rt.ScrollActive = false;
        rt.FitContent = true;
        return rt;
    }

    private static HSeparator CreateSeparator()
    {
        var sep = new HSeparator();
        var style = new StyleBoxFlat { BgColor = BorderDefault };
        style.SetContentMarginAll(1);
        sep.AddThemeStyleboxOverride("separator", style);
        return sep;
    }
}