// ArmyManagementUI.cs
// CRPG风格部队管理+背包面板 — 参考骑马与砍杀/战场兄弟
// 上半：左侧1/3 角色列表+兵种堆叠列表+装备图示+属性 | 右侧2/3 堆叠详情+操作
// 下半（全宽）：暗黑2风格网格背包，支持拖拽整理
// 尺寸 1000×720 居中弹窗
using Godot;
using Godot.Collections;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class ArmyManagementUI : PanelContainer
{
    [Signal] public delegate void CloseRequestedEventHandler();
    [Signal] public delegate void ChangeSchemeEventHandler(string stackId, string newScheme);
    [Signal] public delegate void DismissSoldiersEventHandler(string stackId, int count);
    [Signal] public delegate void CharacterSelectedEventHandler(int characterIndex);
    [Signal] public delegate void EquipFromInventoryEventHandler(string instanceId, string slotId);
    [Signal] public delegate void EquipSlotRightClickedEventHandler(string slotId);
    [Signal] public delegate void InventoryItemClickedEventHandler(string instanceId);
    [Signal] public delegate void InventoryItemDroppedEventHandler(string instanceId, int gridX, int gridY);
    [Signal] public delegate void InventoryItemRightClickedEventHandler(string instanceId);
    [Signal] public delegate void InventorySortRequestedEventHandler();

    // ============================================================================
    // 主题常量
    // ============================================================================
    private static readonly Color BgPrimary = new(0.06f, 0.06f, 0.08f, 0.95f);
    private static readonly Color BgSecondary = new(0.10f, 0.10f, 0.12f, 0.9f);
    private static readonly Color BgCard = new(0.12f, 0.11f, 0.15f, 0.9f);
    private static readonly Color BgGrid = new(0.04f, 0.04f, 0.05f, 0.95f);
    private static readonly Color BgCell = new(0.08f, 0.08f, 0.10f, 0.85f);
    private static readonly Color BgCellHover = new(0.14f, 0.13f, 0.18f, 0.9f);
    private static readonly Color BgCellValid = new(0.08f, 0.22f, 0.08f, 0.7f);
    private static readonly Color BgCellInvalid = new(0.28f, 0.06f, 0.06f, 0.7f);
    private static readonly Color BgItemNormal = new(0.13f, 0.12f, 0.16f, 0.95f);
    private static readonly Color BorderDefault = new(0.3f, 0.3f, 0.35f, 0.6f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color BorderDrag = new(0.85f, 0.72f, 0.25f, 0.95f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextNegative = new(0.9f, 0.3f, 0.25f);
    private static readonly Color TextWarning = new(0.9f, 0.7f, 0.2f);

    private const int FontLarge = 16;
    private const int FontMed = 14;
    private const int FontSmall = 12;
    private const int FontTiny = 11;
    private const int FontMicro = 9;
    private const int SlotSize = 40;
    private const int CellSize = 44;        // 网格背包每格像素
    private const int CellGap = 2;          // 格间距
    private const int PanelWidth = 1000;
    private const int PanelHeight = 720;
    private const int Spacing2 = 4;
    private const int Spacing3 = 6;
    private const int Spacing4 = 8;
    private const int Spacing6 = 12;

    // ============================================================================
    // 装备方案数据
    // ============================================================================
    private static readonly Dictionary<string, Dictionary> EquipSchemes = new()
    {
        ["militia"] = new Dictionary { ["name"] = "民兵", ["weapon"] = "短剑", ["armor"] = "皮甲", ["mount"] = "", ["shield"] = "", ["cost"] = 5, ["identity"] = "轻步兵" },
        ["spearman"] = new Dictionary { ["name"] = "枪兵", ["weapon"] = "长枪", ["armor"] = "锁甲", ["mount"] = "", ["shield"] = "", ["cost"] = 15, ["identity"] = "反骑步兵" },
        ["sword_shield"] = new Dictionary { ["name"] = "剑盾兵", ["weapon"] = "长剑", ["armor"] = "锁甲", ["mount"] = "", ["shield"] = "铁盾", ["cost"] = 18, ["identity"] = "防御步兵" },
        ["archer"] = new Dictionary { ["name"] = "弓手", ["weapon"] = "长弓+匕首", ["armor"] = "皮甲", ["mount"] = "", ["shield"] = "", ["cost"] = 20, ["identity"] = "远程步兵" },
        ["crossbowman"] = new Dictionary { ["name"] = "弩手", ["weapon"] = "十字弩+短剑", ["armor"] = "锁甲", ["mount"] = "", ["shield"] = "", ["cost"] = 22, ["identity"] = "重装远程" },
        ["light_cav"] = new Dictionary { ["name"] = "轻骑", ["weapon"] = "长剑", ["armor"] = "皮甲", ["mount"] = "军马", ["shield"] = "", ["cost"] = 25, ["identity"] = "骑兵" },
        ["lance_cav"] = new Dictionary { ["name"] = "枪骑", ["weapon"] = "长枪", ["armor"] = "锁甲", ["mount"] = "军马", ["shield"] = "", ["cost"] = 30, ["identity"] = "反骑骑兵" },
        ["horse_archer"] = new Dictionary { ["name"] = "骑射手", ["weapon"] = "短弓+匕首", ["armor"] = "皮甲", ["mount"] = "军马", ["shield"] = "", ["cost"] = 28, ["identity"] = "远程骑兵" },
        ["heavy_cav"] = new Dictionary { ["name"] = "重骑", ["weapon"] = "长剑", ["armor"] = "板甲", ["mount"] = "战马", ["shield"] = "铁盾", ["cost"] = 55, ["identity"] = "重装骑兵" },
        ["mage_corps"] = new Dictionary { ["name"] = "法师团", ["weapon"] = "法杖", ["armor"] = "皮甲", ["mount"] = "", ["shield"] = "", ["cost"] = 30, ["identity"] = "法术单位" },
    };

    // ============================================================================
    // 字段
    // ============================================================================
    private VBoxContainer _characterList = null!;
    private VBoxContainer _stackList = null!;
    private VBoxContainer _detailContent = null!;
    private Label _totalArmyLabel = null!;
    private Label _upkeepLabel = null!;
    private Label _stackNameLabel = null!;
    private string _selectedStackId = "";
    private int _selectedCharIndex = -1;
    private readonly System.Collections.Generic.Dictionary<string, PanelContainer> _equipSlots = new();
    private readonly System.Collections.Generic.Dictionary<string, Label> _stackStatLabels = new();

    // 网格背包字段
    private GridInventory? _gridInventory;
    private Control _gridArea = null!;
    private Label _capacityLabel = null!;
    private Label _invInfoLabel = null!;
    private PanelContainer _tooltipPanel = null!;
    private RichTextLabel _tooltipText = null!;
    private readonly System.Collections.Generic.Dictionary<Vector2I, Panel> _cellPanels = new();
    private readonly System.Collections.Generic.Dictionary<string, Control> _itemControls = new();

    // 拖拽状态
    private GridItem? _draggedItem;
    private Control? _dragGhost;
    private Vector2 _dragOffset;
    private bool _isDragging;
    private int _hoverCellX = -1;
    private int _hoverCellY = -1;

    public override void _Ready()
    {
        Setup();
        Visible = false;
    }

    // ============================================================================
    // UI构建
    // ============================================================================
    private void Setup()
    {
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        CustomMinimumSize = new Vector2(PanelWidth, PanelHeight);
        Size = new Vector2(PanelWidth, PanelHeight);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = BgPrimary;
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = BorderHighlight;
        panelStyle.SetCornerRadiusAll(4);
        panelStyle.ShadowColor = new Color(0, 0, 0, 0.7f);
        panelStyle.ShadowSize = 8;
        AddThemeStyleboxOverride("panel", panelStyle);

        var rootMargin = new MarginContainer();
        rootMargin.AddThemeConstantOverride("margin_left", Spacing6);
        rootMargin.AddThemeConstantOverride("margin_right", Spacing6);
        rootMargin.AddThemeConstantOverride("margin_top", Spacing4);
        rootMargin.AddThemeConstantOverride("margin_bottom", Spacing4);
        AddChild(rootMargin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", Spacing3);
        rootMargin.AddChild(mainVbox);

        // 顶部标题栏
        BuildHeader(mainVbox);
        mainVbox.AddChild(MakeSep());

        // 上半主体：左1/3 + 右2/3（不含背包）
        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", Spacing6);
        body.CustomMinimumSize = new Vector2(0, 280);
        mainVbox.AddChild(body);

        BuildLeftColumn(body);

        var vsep = new VSeparator();
        vsep.AddThemeStyleboxOverride("separator", MakeThinSep());
        body.AddChild(vsep);

        BuildRightColumn(body);

        // 分隔线 + 下半全宽网格背包
        mainVbox.AddChild(MakeSep());
        BuildGridInventory(mainVbox);

        // 浮动Tooltip层
        BuildTooltip();
    }

    private void BuildHeader(VBoxContainer parent)
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", Spacing4);
        parent.AddChild(header);

        var title = MakeLabel("部队与物资", FontLarge, TextAccent);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(title);

        _totalArmyLabel = MakeLabel("总兵力: 0", FontSmall, TextAccent);
        header.AddChild(_totalArmyLabel);

        _upkeepLabel = MakeLabel("日薪俸: 0金", FontSmall, TextWarning);
        header.AddChild(_upkeepLabel);

        var closeBtn = MakeBtn("✕", 24);
        closeBtn.Pressed += () => { Visible = false; EmitSignal(SignalName.CloseRequested); };
        header.AddChild(closeBtn);
    }

    // ============================================================================
    // 左栏：角色列表 + 部队编制 + 装备图示 + 属性
    // ============================================================================
    private void BuildLeftColumn(HBoxContainer body)
    {
        var leftCol = new VBoxContainer();
        leftCol.CustomMinimumSize = new Vector2(280, 0);
        leftCol.AddThemeConstantOverride("separation", Spacing3);
        body.AddChild(leftCol);

        // ── 角色列表 ──
        leftCol.AddChild(MakeLabel("角色", FontMed, TextAccent));

        var charScroll = new ScrollContainer();
        charScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
        charScroll.VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        charScroll.CustomMinimumSize = new Vector2(0, 52);
        leftCol.AddChild(charScroll);

        _characterList = new VBoxContainer();
        // 实际用HBox横向排列角色头像
        var charRow = new HBoxContainer();
        charRow.AddThemeConstantOverride("separation", Spacing2);
        charScroll.AddChild(charRow);
        // _characterList 用于外部刷新引用，挂在charRow上
        _characterList = new VBoxContainer(); // placeholder，实际角色条目加到charRow
        _characterList.SetMeta("char_row", charRow);

        leftCol.AddChild(MakeSep());

        // ── 部队编制 ──
        leftCol.AddChild(MakeLabel("部队编制", FontMed, TextAccent));

        var stackScroll = new ScrollContainer();
        stackScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        stackScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        stackScroll.CustomMinimumSize = new Vector2(0, 100);
        leftCol.AddChild(stackScroll);

        _stackList = new VBoxContainer();
        _stackList.AddThemeConstantOverride("separation", Spacing2);
        stackScroll.AddChild(_stackList);

        leftCol.AddChild(MakeSep());

        // 选中堆叠装备图示
        BuildEquipDisplay(leftCol);

        leftCol.AddChild(MakeSep());

        // 堆叠名
        _stackNameLabel = MakeLabel("选择一个堆叠", FontSmall, TextPrimary);
        _stackNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        leftCol.AddChild(_stackNameLabel);

        leftCol.AddChild(MakeSep());

        // 战斗属性
        BuildStackStats(leftCol);
    }

    private void BuildEquipDisplay(VBoxContainer parent)
    {
        // 纸娃娃布局：类骑砍2/战场兄弟风格
        // 行1:        [头盔]
        // 行2: [主手] [铠甲] [副手]
        // 行3: [护手]        [饰品1]
        // 行4:        [鞋子]
        // 行5: [副武器主] [饰品2] [副武器副]

        var equipContainer = new VBoxContainer();
        equipContainer.AddThemeConstantOverride("separation", Spacing2);
        parent.AddChild(equipContainer);

        // 行1: 头盔居中
        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 0);
        row1.Alignment = BoxContainer.AlignmentMode.Center;
        equipContainer.AddChild(row1);
        row1.AddChild(MakeEquipSlot("helmet", "头盔", "🪖"));

        // 行2: 主手 | 铠甲 | 副手
        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", Spacing3);
        row2.Alignment = BoxContainer.AlignmentMode.Center;
        equipContainer.AddChild(row2);
        row2.AddChild(MakeEquipSlot("primary_main", "主手", "⚔"));
        row2.AddChild(MakeEquipSlot("armor", "铠甲", "🛡"));
        row2.AddChild(MakeEquipSlot("primary_off", "副手", "🗡"));

        // 行3: 护手 | 空白 | 饰品1
        var row3 = new HBoxContainer();
        row3.AddThemeConstantOverride("separation", Spacing3);
        row3.Alignment = BoxContainer.AlignmentMode.Center;
        equipContainer.AddChild(row3);
        row3.AddChild(MakeEquipSlot("gauntlets", "护手", "🧤"));
        // 中间占位
        var spacer3 = new Control();
        spacer3.CustomMinimumSize = new Vector2(SlotSize, 0);
        row3.AddChild(spacer3);
        row3.AddChild(MakeEquipSlot("accessory_1", "饰品", "💍"));

        // 行4: 鞋子居中
        var row4 = new HBoxContainer();
        row4.AddThemeConstantOverride("separation", 0);
        row4.Alignment = BoxContainer.AlignmentMode.Center;
        equipContainer.AddChild(row4);
        row4.AddChild(MakeEquipSlot("boots", "鞋子", "👢"));

        // 行5: 副武器主 | 饰品2 | 副武器副
        var row5 = new HBoxContainer();
        row5.AddThemeConstantOverride("separation", Spacing3);
        row5.Alignment = BoxContainer.AlignmentMode.Center;
        equipContainer.AddChild(row5);
        row5.AddChild(MakeEquipSlot("secondary_main", "副武器", "⚔"));
        row5.AddChild(MakeEquipSlot("accessory_2", "饰品", "💎"));
        row5.AddChild(MakeEquipSlot("secondary_off", "副副手", "🗡"));
    }

    /// <summary>创建装备槽位（图标风格，支持拖入装备）</summary>
    private PanelContainer MakeEquipSlot(string slotId, string slotLabel, string icon)
    {
        var slot = new PanelContainer();
        slot.CustomMinimumSize = new Vector2(SlotSize + 6, SlotSize + 6);
        slot.TooltipText = slotLabel;
        slot.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        var style = new StyleBoxFlat { BgColor = new Color(0.09f, 0.08f, 0.11f, 0.9f) };
        style.SetBorderWidthAll(1);
        style.BorderColor = new Color(0.28f, 0.26f, 0.3f, 0.7f);
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(2);
        slot.AddThemeStyleboxOverride("panel", style);

        // 图标纹理区域（装备后显示物品图标）
        var iconRect = new TextureRect();
        iconRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        iconRect.OffsetLeft = 3;
        iconRect.OffsetTop = 3;
        iconRect.OffsetRight = -3;
        iconRect.OffsetBottom = -12;
        iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        slot.AddChild(iconRect);

        // 占位图标符号（无装备时显示）
        var placeholderLbl = new Label();
        placeholderLbl.Text = icon;
        placeholderLbl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        placeholderLbl.HorizontalAlignment = HorizontalAlignment.Center;
        placeholderLbl.VerticalAlignment = VerticalAlignment.Center;
        placeholderLbl.AddThemeFontSizeOverride("font_size", 14);
        placeholderLbl.AddThemeColorOverride("font_color", new Color(0.35f, 0.33f, 0.38f, 0.7f));
        placeholderLbl.MouseFilter = Control.MouseFilterEnum.Ignore;
        slot.AddChild(placeholderLbl);

        // 底部槽位名
        var nameLbl = new Label();
        nameLbl.Text = slotLabel;
        nameLbl.AddThemeFontSizeOverride("font_size", FontMicro);
        nameLbl.AddThemeColorOverride("font_color", TextMuted);
        nameLbl.HorizontalAlignment = HorizontalAlignment.Center;
        nameLbl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        nameLbl.OffsetBottom = 0;
        nameLbl.OffsetTop = -11;
        nameLbl.MouseFilter = Control.MouseFilterEnum.Ignore;
        slot.AddChild(nameLbl);

        // 存储元数据
        slot.SetMeta("slot_id", slotId);
        slot.SetMeta("icon_rect", iconRect);
        slot.SetMeta("placeholder", placeholderLbl);
        slot.SetMeta("name_label", nameLbl);

        // 拖入装备事件
        slot.GuiInput += (ev) => OnEquipSlotInput(ev, slotId, slot);

        _equipSlots[slotId] = slot;
        return slot;
    }

    /// <summary>装备槽输入处理（支持从背包拖入）</summary>
    private void OnEquipSlotInput(InputEvent ev, string slotId, PanelContainer slot)
    {
        if (ev is InputEventMouseButton mouseBtn)
        {
            if (mouseBtn.ButtonIndex == MouseButton.Left && !mouseBtn.Pressed && _isDragging)
            {
                // 从背包拖入装备槽
                TryEquipDraggedItem(slotId);
                GetViewport().SetInputAsHandled();
            }
            else if (mouseBtn.ButtonIndex == MouseButton.Right && mouseBtn.Pressed)
            {
                // 右键卸下装备
                EmitSignal(SignalName.EquipSlotRightClicked, slotId);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    /// <summary>尝试将拖拽中的物品装备到指定槽位</summary>
    private void TryEquipDraggedItem(string slotId)
    {
        if (_draggedItem == null || _gridInventory == null) return;

        // 发出装备信号，由外部控制器处理实际装备逻辑
        EmitSignal(SignalName.EquipFromInventory, _draggedItem.InstanceId, slotId);
        CancelDrag();
    }

    /// <summary>更新装备槽显示（外部调用，传入当前角色装备数据）</summary>
    public void UpdateEquipSlot(string slotId, ItemData? item)
    {
        if (!_equipSlots.TryGetValue(slotId, out var slot)) return;

        var iconRect = slot.GetMeta("icon_rect").As<TextureRect>();
        var placeholder = slot.GetMeta("placeholder").As<Label>();
        var nameLbl = slot.GetMeta("name_label").As<Label>();

        if (item != null)
        {
            // 有装备：显示图标，隐藏占位符，更新边框颜色
            if (placeholder != null) placeholder.Visible = false;
            if (nameLbl != null)
            {
                string displayName = item.ItemName.Length > 4 ? item.ItemName[..4] : item.ItemName;
                nameLbl.Text = displayName;
                nameLbl.AddThemeColorOverride("font_color", item.GetRarityColor());
            }

            // 尝试加载图标
            if (iconRect != null && !string.IsNullOrEmpty(item.IconId))
            {
                var tex = GD.Load<Texture2D>(item.IconId);
                if (tex != null) iconRect.Texture = tex;
            }

            // 稀有度边框
            var rarityColor = item.GetRarityColor();
            var style = new StyleBoxFlat { BgColor = new Color(0.1f, 0.09f, 0.13f, 0.95f) };
            style.SetBorderWidthAll(1);
            style.BorderColor = new Color(rarityColor.R * 0.8f, rarityColor.G * 0.8f, rarityColor.B * 0.8f, 0.9f);
            style.SetCornerRadiusAll(4);
            style.SetContentMarginAll(2);
            slot.AddThemeStyleboxOverride("panel", style);

            slot.TooltipText = item.GetFullName();
        }
        else
        {
            // 无装备：恢复默认
            if (placeholder != null) placeholder.Visible = true;
            if (iconRect != null) iconRect.Texture = null;
            if (nameLbl != null)
            {
                nameLbl.AddThemeColorOverride("font_color", TextMuted);
            }

            var style = new StyleBoxFlat { BgColor = new Color(0.09f, 0.08f, 0.11f, 0.9f) };
            style.SetBorderWidthAll(1);
            style.BorderColor = new Color(0.28f, 0.26f, 0.3f, 0.7f);
            style.SetCornerRadiusAll(4);
            style.SetContentMarginAll(2);
            slot.AddThemeStyleboxOverride("panel", style);
        }
    }

    private void BuildStackStats(VBoxContainer parent)
    {
        var grid = new GridContainer();
        grid.Columns = 4;
        grid.AddThemeConstantOverride("h_separation", Spacing3);
        grid.AddThemeConstantOverride("v_separation", 2);
        parent.AddChild(grid);

        AddStatCell(grid, "s_atk", "攻击");
        AddStatCell(grid, "s_def", "防御");
        AddStatCell(grid, "s_hp", "生命");
        AddStatCell(grid, "s_spd", "速度");
        AddStatCell(grid, "s_rng", "射程");
        AddStatCell(grid, "s_mor", "士气");
    }

    private void AddStatCell(GridContainer parent, string id, string name)
    {
        parent.AddChild(MakeLabel(name, FontTiny, TextMuted));
        var val = MakeLabel("—", FontSmall, TextPrimary);
        val.CustomMinimumSize = new Vector2(24, 0);
        parent.AddChild(val);
        _stackStatLabels[id] = val;
    }

    // ============================================================================
    // 右栏：详情+操作（不含背包，背包移到下方全宽）
    // ============================================================================
    private void BuildRightColumn(HBoxContainer body)
    {
        var rightCol = new VBoxContainer();
        rightCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rightCol.AddThemeConstantOverride("separation", Spacing3);
        body.AddChild(rightCol);

        // 详情面板
        var detailPanel = new PanelContainer();
        var dStyle = new StyleBoxFlat { BgColor = BgSecondary };
        dStyle.SetBorderWidthAll(1);
        dStyle.BorderColor = BorderDefault;
        dStyle.SetCornerRadiusAll(3);
        dStyle.SetContentMarginAll(Spacing4);
        detailPanel.AddThemeStyleboxOverride("panel", dStyle);
        detailPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        rightCol.AddChild(detailPanel);

        var detailScroll = new ScrollContainer();
        detailScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        detailScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        detailPanel.AddChild(detailScroll);

        _detailContent = new VBoxContainer();
        _detailContent.AddThemeConstantOverride("separation", Spacing3);
        detailScroll.AddChild(_detailContent);

        _detailContent.AddChild(MakeLabel("选择左侧堆叠查看详情", FontSmall, TextMuted));

        // 操作栏：换装方案
        rightCol.AddChild(MakeSep());
        BuildSchemeBar(rightCol);
    }

    private void BuildSchemeBar(VBoxContainer parent)
    {
        parent.AddChild(MakeLabel("装备方案 (点击切换选中堆叠)", FontTiny, TextSecondary));

        var schemeScroll = new ScrollContainer();
        schemeScroll.CustomMinimumSize = new Vector2(0, 32);
        schemeScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
        schemeScroll.VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        parent.AddChild(schemeScroll);

        var schemeRow = new HBoxContainer();
        schemeRow.AddThemeConstantOverride("separation", Spacing2);
        schemeScroll.AddChild(schemeRow);

        foreach (var kvp in EquipSchemes)
        {
            string schemeId = kvp.Key;
            var scheme = kvp.Value;
            string schemeName = scheme["name"].AsString();
            string identity = scheme["identity"].AsString();
            int cost = scheme["cost"].AsInt32();

            var btn = MakeBtn(schemeName, 56, 26);
            btn.TooltipText = $"{schemeName} ({identity}) - {cost}金/人";
            string captured = schemeId;
            btn.Pressed += () =>
            {
                if (!string.IsNullOrEmpty(_selectedStackId))
                    EmitSignal(SignalName.ChangeScheme, _selectedStackId, captured);
            };
            schemeRow.AddChild(btn);
        }

        // 解雇按钮
        var dismissBtn = MakeBtn("解雇", 50, 26);
        dismissBtn.AddThemeColorOverride("font_color", TextNegative);
        dismissBtn.Pressed += () =>
        {
            if (!string.IsNullOrEmpty(_selectedStackId))
                EmitSignal(SignalName.DismissSoldiers, _selectedStackId, 1);
        };
        schemeRow.AddChild(dismissBtn);
    }

    // ============================================================================
    // 下方全宽：暗黑2风格网格背包
    // ============================================================================
    private void BuildGridInventory(VBoxContainer parent)
    {
        // 标题行：背包标签 + 容量 + 整理按钮
        var invHeader = new HBoxContainer();
        invHeader.AddThemeConstantOverride("separation", Spacing4);
        parent.AddChild(invHeader);

        var invIcon = MakeLabel("⬛", FontMed, new Color(0.6f, 0.55f, 0.4f));
        invHeader.AddChild(invIcon);

        var invTitle = MakeLabel("背包", FontMed, TextAccent);
        invHeader.AddChild(invTitle);

        _capacityLabel = MakeLabel("容量: —/—", FontSmall, TextSecondary);
        _capacityLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        invHeader.AddChild(_capacityLabel);

        _invInfoLabel = MakeLabel("", FontTiny, TextMuted);
        invHeader.AddChild(_invInfoLabel);

        var sortBtn = MakeBtn("整理", 52, 22);
        sortBtn.TooltipText = "自动整理背包（按物品大小排列）";
        sortBtn.Pressed += OnSortInventory;
        invHeader.AddChild(sortBtn);

        // 网格面板（带装饰边框和内阴影效果）
        var gridOuter = new PanelContainer();
        var outerStyle = new StyleBoxFlat();
        outerStyle.BgColor = BgGrid;
        outerStyle.SetBorderWidthAll(1);
        outerStyle.BorderColor = new Color(0.22f, 0.20f, 0.18f, 0.8f);
        outerStyle.SetCornerRadiusAll(4);
        outerStyle.SetContentMarginAll(6);
        // 内阴影效果
        outerStyle.ShadowColor = new Color(0, 0, 0, 0.3f);
        outerStyle.ShadowSize = 3;
        outerStyle.ShadowOffset = new Vector2(0, 1);
        gridOuter.AddThemeStyleboxOverride("panel", outerStyle);
        gridOuter.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        parent.AddChild(gridOuter);

        // 可滚动容器
        var gridScroll = new ScrollContainer();
        gridScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        gridScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        gridScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        gridScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        // 自定义滚动条样式
        gridScroll.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        gridOuter.AddChild(gridScroll);

        // 网格绘制区域（绝对定位子控件）
        _gridArea = new Control();
        _gridArea.MouseFilter = Control.MouseFilterEnum.Stop;
        _gridArea.GuiInput += OnGridAreaInput;
        _gridArea.MouseExited += () => { _hoverCellX = -1; _hoverCellY = -1; ClearCellHighlights(); };
        gridScroll.AddChild(_gridArea);

        // 底部信息栏
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", Spacing4);
        parent.AddChild(footer);

        var helpLbl = MakeLabel("左键拖拽移动 · 右键使用/查看 · 滚轮滚动", FontTiny, TextMuted);
        helpLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        footer.AddChild(helpLbl);
    }

    private void BuildTooltip()
    {
        _tooltipPanel = new PanelContainer();
        _tooltipPanel.Visible = false;
        _tooltipPanel.ZIndex = 100;
        _tooltipPanel.MouseFilter = Control.MouseFilterEnum.Ignore;

        var ttStyle = new StyleBoxFlat();
        ttStyle.BgColor = new Color(0.04f, 0.04f, 0.06f, 0.96f);
        ttStyle.SetBorderWidthAll(1);
        ttStyle.BorderColor = new Color(0.45f, 0.4f, 0.3f, 0.9f);
        ttStyle.SetCornerRadiusAll(4);
        ttStyle.SetContentMarginAll(10);
        ttStyle.ShadowColor = new Color(0, 0, 0, 0.5f);
        ttStyle.ShadowSize = 4;
        _tooltipPanel.AddThemeStyleboxOverride("panel", ttStyle);

        _tooltipText = new RichTextLabel();
        _tooltipText.BbcodeEnabled = true;
        _tooltipText.ScrollActive = false;
        _tooltipText.FitContent = true;
        _tooltipText.CustomMinimumSize = new Vector2(220, 0);
        _tooltipText.MouseFilter = Control.MouseFilterEnum.Ignore;
        _tooltipPanel.AddChild(_tooltipText);

        AddChild(_tooltipPanel);
    }

    // ============================================================================
    // 网格背包：构建/刷新
    // ============================================================================

    private void RebuildGridCells()
    {
        // 清除旧格子
        foreach (var cell in _cellPanels.Values)
            cell.QueueFree();
        _cellPanels.Clear();
        ClearItemControls();

        if (_gridInventory == null) return;

        int gw = _gridInventory.GridWidth;
        int gh = _gridInventory.GridHeight;
        int totalW = gw * (CellSize + CellGap) - CellGap;
        int totalH = gh * (CellSize + CellGap) - CellGap;
        _gridArea.CustomMinimumSize = new Vector2(totalW, totalH);

        // 绘制格子背景（棋盘微妙交替色）
        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                var cell = new Panel();
                cell.Position = new Vector2(x * (CellSize + CellGap), y * (CellSize + CellGap));
                cell.Size = new Vector2(CellSize, CellSize);
                cell.MouseFilter = Control.MouseFilterEnum.Ignore;

                // 微妙棋盘色差
                bool isAlt = (x + y) % 2 == 1;
                var cellBg = isAlt
                    ? new Color(BgCell.R + 0.015f, BgCell.G + 0.015f, BgCell.B + 0.02f, BgCell.A)
                    : BgCell;

                var cellStyle = new StyleBoxFlat { BgColor = cellBg };
                cellStyle.SetBorderWidthAll(1);
                cellStyle.BorderColor = new Color(0.2f, 0.2f, 0.22f, 0.4f);
                cellStyle.SetCornerRadiusAll(1);
                cell.AddThemeStyleboxOverride("panel", cellStyle);

                _gridArea.AddChild(cell);
                _cellPanels[new Vector2I(x, y)] = cell;
            }
        }
    }

    private void RefreshGridItems()
    {
        ClearItemControls();
        if (_gridInventory == null) return;

        foreach (var gridItem in _gridInventory.Items)
            CreateGridItemControl(gridItem);

        UpdateInventoryInfo();
    }

    private void CreateGridItemControl(GridItem gridItem)
    {
        var control = new Panel();
        int px = gridItem.GridX * (CellSize + CellGap);
        int py = gridItem.GridY * (CellSize + CellGap);
        int pw = gridItem.Width * (CellSize + CellGap) - CellGap;
        int ph = gridItem.Height * (CellSize + CellGap) - CellGap;

        control.Position = new Vector2(px, py);
        control.Size = new Vector2(pw, ph);
        control.MouseFilter = Control.MouseFilterEnum.Stop;
        control.ZIndex = 1;
        control.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        // 物品背景 — 根据稀有度渐变底色
        var rarityColor = gridItem.Item.GetRarityColor();
        var itemBg = new Color(
            BgItemNormal.R + rarityColor.R * 0.04f,
            BgItemNormal.G + rarityColor.G * 0.04f,
            BgItemNormal.B + rarityColor.B * 0.04f,
            BgItemNormal.A);

        var itemStyle = new StyleBoxFlat { BgColor = itemBg };
        itemStyle.SetBorderWidthAll(1);
        itemStyle.BorderColor = new Color(rarityColor.R * 0.7f, rarityColor.G * 0.7f, rarityColor.B * 0.7f, 0.85f);
        itemStyle.SetCornerRadiusAll(2);
        control.AddThemeStyleboxOverride("panel", itemStyle);

        // 物品图标
        var iconRect = new TextureRect();
        iconRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        iconRect.OffsetLeft = 3;
        iconRect.OffsetTop = 3;
        iconRect.OffsetRight = -3;
        iconRect.OffsetBottom = -14; // 留出底部名称空间
        iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        control.AddChild(iconRect);

        // 尝试加载图标
        if (!string.IsNullOrEmpty(gridItem.Item.IconId))
        {
            var tex = GD.Load<Texture2D>(gridItem.Item.IconId);
            if (tex != null) iconRect.Texture = tex;
        }

        // 底部物品名（截断显示）
        var nameLabel = new Label();
        int maxChars = gridItem.Width * 3;
        string displayName = gridItem.Item.ItemName.Length > maxChars
            ? gridItem.Item.ItemName[..maxChars]
            : gridItem.Item.ItemName;
        nameLabel.Text = displayName;
        nameLabel.AddThemeFontSizeOverride("font_size", FontMicro);
        nameLabel.AddThemeColorOverride("font_color", rarityColor);
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        nameLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        nameLabel.OffsetBottom = -1;
        nameLabel.OffsetTop = -12;
        nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        control.AddChild(nameLabel);

        // 堆叠数量角标
        if (gridItem.Quantity > 1)
        {
            var qtyLabel = new Label();
            qtyLabel.Text = $"×{gridItem.Quantity}";
            qtyLabel.AddThemeFontSizeOverride("font_size", FontTiny);
            qtyLabel.AddThemeColorOverride("font_color", TextPrimary);
            qtyLabel.HorizontalAlignment = HorizontalAlignment.Right;
            qtyLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
            qtyLabel.OffsetRight = -2;
            qtyLabel.OffsetTop = 1;
            qtyLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            control.AddChild(qtyLabel);
        }

        // 事件绑定
        control.GuiInput += (ev) => OnItemInput(ev, gridItem, control);
        control.MouseEntered += () => ShowItemTooltip(gridItem, control);
        control.MouseExited += () => HideTooltip();

        control.SetMeta("instance_id", gridItem.InstanceId);
        _gridArea.AddChild(control);
        _itemControls[gridItem.InstanceId] = control;
    }

    private void ClearItemControls()
    {
        foreach (var ctrl in _itemControls.Values)
            ctrl.QueueFree();
        _itemControls.Clear();
    }

    private void UpdateInventoryInfo()
    {
        if (_gridInventory == null) return;
        int used = _gridInventory.UsedCells;
        int total = _gridInventory.TotalCells;
        float pct = total > 0 ? (float)used / total * 100f : 0;
        _capacityLabel.Text = $"容量: {used}/{total} ({pct:F0}%)";
        _invInfoLabel.Text = $"物品 {_gridInventory.TotalItemCount}件 · 价值 {_gridInventory.TotalValue}金";
    }

    // ============================================================================
    // 拖拽逻辑
    // ============================================================================

    private void OnItemInput(InputEvent ev, GridItem gridItem, Control control)
    {
        if (ev is InputEventMouseButton mouseBtn)
        {
            if (mouseBtn.ButtonIndex == MouseButton.Left && mouseBtn.Pressed)
            {
                if (mouseBtn.DoubleClick)
                {
                    EmitSignal(SignalName.InventoryItemClicked, gridItem.InstanceId);
                    return;
                }
                StartDrag(gridItem, control, mouseBtn.GlobalPosition);
                GetViewport().SetInputAsHandled();
            }
            else if (mouseBtn.ButtonIndex == MouseButton.Right && mouseBtn.Pressed)
            {
                EmitSignal(SignalName.InventoryItemRightClicked, gridItem.InstanceId);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void StartDrag(GridItem gridItem, Control control, Vector2 mousePos)
    {
        _isDragging = true;
        _draggedItem = gridItem;
        _dragOffset = control.GlobalPosition - mousePos;

        // 创建拖拽幽灵
        _dragGhost = new Panel();
        _dragGhost.Size = control.Size;
        _dragGhost.ZIndex = 80;
        _dragGhost.MouseFilter = Control.MouseFilterEnum.Ignore;
        _dragGhost.Modulate = new Color(1, 1, 1, 0.75f);

        var ghostStyle = new StyleBoxFlat();
        ghostStyle.BgColor = new Color(0.18f, 0.15f, 0.25f, 0.85f);
        ghostStyle.SetBorderWidthAll(2);
        ghostStyle.BorderColor = BorderDrag;
        ghostStyle.SetCornerRadiusAll(3);
        _dragGhost.AddThemeStyleboxOverride("panel", ghostStyle);

        // 幽灵中显示物品名
        var ghostLabel = new Label();
        ghostLabel.Text = gridItem.Item.ItemName;
        ghostLabel.AddThemeFontSizeOverride("font_size", FontSmall);
        ghostLabel.AddThemeColorOverride("font_color", gridItem.Item.GetRarityColor());
        ghostLabel.HorizontalAlignment = HorizontalAlignment.Center;
        ghostLabel.VerticalAlignment = VerticalAlignment.Center;
        ghostLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ghostLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _dragGhost.AddChild(ghostLabel);

        AddChild(_dragGhost);
        _dragGhost.GlobalPosition = mousePos + _dragOffset;

        // 半透明原位物品
        control.Modulate = new Color(1, 1, 1, 0.25f);
        HideTooltip();
    }

    private void CancelDrag()
    {
        if (!_isDragging) return;
        _isDragging = false;

        if (_dragGhost != null)
        {
            _dragGhost.QueueFree();
            _dragGhost = null;
        }

        if (_draggedItem != null && _itemControls.TryGetValue(_draggedItem.InstanceId, out var ctrl))
            ctrl.Modulate = Colors.White;

        _draggedItem = null;
        ClearCellHighlights();
    }

    private void CompleteDrag(int targetX, int targetY)
    {
        if (_gridInventory == null || _draggedItem == null) return;

        var targetItem = _gridInventory.GetItemAt(targetX, targetY);
        bool success;

        if (targetItem != null && targetItem != _draggedItem)
            success = _gridInventory.TrySwap(_draggedItem, targetItem);
        else
            success = _gridInventory.TryMove(_draggedItem, targetX, targetY);

        if (success)
            EmitSignal(SignalName.InventoryItemDropped, _draggedItem.InstanceId, targetX, targetY);

        CancelDrag();
        RefreshGridItems();
    }

    // ============================================================================
    // 网格输入处理
    // ============================================================================

    private void OnGridAreaInput(InputEvent ev)
    {
        if (ev is InputEventMouseMotion motion)
        {
            var cell = PositionToCell(motion.Position);
            _hoverCellX = cell.X;
            _hoverCellY = cell.Y;

            if (_isDragging && _dragGhost != null)
            {
                _dragGhost.GlobalPosition = motion.GlobalPosition + _dragOffset;
                UpdateDragHighlight();
            }
        }
        else if (ev is InputEventMouseButton mouseBtn)
        {
            if (mouseBtn.ButtonIndex == MouseButton.Left && !mouseBtn.Pressed && _isDragging)
            {
                var cellPos = PositionToCell(mouseBtn.Position);
                if (cellPos.X >= 0 && cellPos.Y >= 0)
                    CompleteDrag(cellPos.X, cellPos.Y);
                else
                    CancelDrag();
            }
        }
    }

    public override void _Input(InputEvent ev)
    {
        if (!Visible || !_isDragging) return;

        if (ev is InputEventMouseMotion motion && _dragGhost != null)
            _dragGhost.GlobalPosition = motion.GlobalPosition + _dragOffset;
        else if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
            CancelDrag();
        else if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
            CancelDrag();
    }

    // ============================================================================
    // 格子高亮
    // ============================================================================

    private void UpdateDragHighlight()
    {
        ClearCellHighlights();
        if (_gridInventory == null || _draggedItem == null) return;
        if (_hoverCellX < 0 || _hoverCellY < 0) return;

        int w = _draggedItem.Width;
        int h = _draggedItem.Height;
        bool canPlace = _gridInventory.CanPlaceSize(w, h, _hoverCellX, _hoverCellY, _draggedItem);
        Color hlColor = canPlace ? BgCellValid : BgCellInvalid;
        Color hlBorder = canPlace
            ? new Color(0.25f, 0.7f, 0.25f, 0.85f)
            : new Color(0.7f, 0.2f, 0.2f, 0.85f);

        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                var key = new Vector2I(_hoverCellX + dx, _hoverCellY + dy);
                if (_cellPanels.TryGetValue(key, out var cellPanel))
                {
                    var style = new StyleBoxFlat { BgColor = hlColor };
                    style.SetBorderWidthAll(1);
                    style.BorderColor = hlBorder;
                    style.SetCornerRadiusAll(1);
                    cellPanel.AddThemeStyleboxOverride("panel", style);
                }
            }
        }
    }

    private void ClearCellHighlights()
    {
        if (_gridInventory == null) return;
        foreach (var kvp in _cellPanels)
        {
            bool isAlt = (kvp.Key.X + kvp.Key.Y) % 2 == 1;
            var cellBg = isAlt
                ? new Color(BgCell.R + 0.015f, BgCell.G + 0.015f, BgCell.B + 0.02f, BgCell.A)
                : BgCell;
            var style = new StyleBoxFlat { BgColor = cellBg };
            style.SetBorderWidthAll(1);
            style.BorderColor = new Color(0.2f, 0.2f, 0.22f, 0.4f);
            style.SetCornerRadiusAll(1);
            kvp.Value.AddThemeStyleboxOverride("panel", style);
        }
    }

    // ============================================================================
    // Tooltip
    // ============================================================================

    private void ShowItemTooltip(GridItem gridItem, Control source)
    {
        if (_isDragging) return;

        var item = gridItem.Item;
        var rarityHex = item.GetRarityColor().ToHtml(false);
        string text = $"[color=#{rarityHex}][b]{item.GetFullName()}[/b][/color]\n";
        text += $"[color=#999]{item.GetRarityName()}[/color]";

        if (item is WeaponData weapon)
            text += $"\n\n[color=#ddd]{weapon.GetWeaponDescription()}[/color]";
        else if (item is ArmorData armor)
        {
            text += $"\n\n[color=#ddd]装甲: {armor.MaxArmorPoints} | 穿透阈值: {armor.DrThreshold}[/color]";
            if (armor.MaxDexBonus < 99)
                text += $"\n[color=#ddd]DEX上限: {armor.MaxDexBonus}[/color]";
        }

        if (!string.IsNullOrEmpty(item.Description))
            text += $"\n\n[color=#888i]{item.Description}[/color]";

        text += $"\n\n[color=#666]占用: {item.InvWidth}×{item.InvHeight} 格[/color]";
        text += $"\n[color=#666]价值: {item.GetSellPrice()} 金[/color]";

        if (gridItem.Quantity > 1)
            text += $"\n[color=#666]数量: ×{gridItem.Quantity}[/color]";

        string affixDesc = item.GetAffixDescriptions();
        if (!string.IsNullOrEmpty(affixDesc))
            text += $"\n\n[color=#b07de8]{affixDesc}[/color]";

        _tooltipText.Text = text;
        _tooltipPanel.Visible = true;

        // 定位：物品右侧，避免超出面板
        var ttPos = source.GlobalPosition + new Vector2(source.Size.X + 10, 0);
        _tooltipPanel.GlobalPosition = ttPos;
    }

    private void HideTooltip()
    {
        _tooltipPanel.Visible = false;
    }

    // ============================================================================
    // 辅助
    // ============================================================================

    private Vector2I PositionToCell(Vector2 localPos)
    {
        int x = (int)(localPos.X / (CellSize + CellGap));
        int y = (int)(localPos.Y / (CellSize + CellGap));
        if (_gridInventory == null) return new Vector2I(-1, -1);
        if (x < 0 || x >= _gridInventory.GridWidth || y < 0 || y >= _gridInventory.GridHeight)
            return new Vector2I(-1, -1);
        return new Vector2I(x, y);
    }

    private void OnSortInventory()
    {
        _gridInventory?.AutoSort();
        RefreshGridItems();
        EmitSignal(SignalName.InventorySortRequested);
    }

    // ============================================================================
    // 堆叠点击逻辑
    // ============================================================================
    private void OnStackClicked(string stackId, string schemeId)
    {
        _selectedStackId = stackId;
        UpdateLeftForScheme(schemeId);
        UpdateDetailPanel(stackId, schemeId);
    }

    private void UpdateLeftForScheme(string schemeId)
    {
        if (!EquipSchemes.TryGetValue(schemeId, out var scheme))
        {
            _stackNameLabel.Text = "—";
            return;
        }

        string name = scheme["name"].AsString();
        string weapon = scheme["weapon"].AsString();
        string armor = scheme["armor"].AsString();
        string mount = scheme["mount"].AsString();
        string shield = scheme["shield"].AsString();
        string identity = scheme["identity"].AsString();
        int cost = scheme["cost"].AsInt32();

        _stackNameLabel.Text = $"{name} ({identity})";

        // 部队堆叠选中时不直接更新装备槽（装备槽跟随角色选择）
        // 仅更新战斗属性
        // 模拟战斗属性
        int atk = cost / 3 + 3;
        int def = shield != "" ? cost / 4 + 4 : cost / 5 + 2;
        int hp = 8 + cost / 5;
        int spd = mount != "" ? 6 : 4;
        int rng = weapon.Contains("弓") || weapon.Contains("弩") ? 5 : 1;
        int mor = 50 + cost / 2;

        SetStatVal("s_atk", atk);
        SetStatVal("s_def", def);
        SetStatVal("s_hp", hp);
        SetStatVal("s_spd", spd);
        SetStatVal("s_rng", rng);
        SetStatVal("s_mor", mor);
    }

    private void UpdateDetailPanel(string stackId, string schemeId)
    {
        foreach (Node child in _detailContent.GetChildren())
            child.QueueFree();

        if (!EquipSchemes.TryGetValue(schemeId, out var scheme)) return;

        string name = scheme["name"].AsString();
        string identity = scheme["identity"].AsString();
        int cost = scheme["cost"].AsInt32();

        _detailContent.AddChild(MakeLabel($"▶ {name} 堆叠", FontMed, TextPrimary));

        var info = new RichTextLabel();
        info.BbcodeEnabled = true;
        info.ScrollActive = false;
        info.FitContent = true;
        info.Text = $"[color=#ccc]兵种定位: {identity}[/color]\n" +
                    $"[color=#ccc]单兵费用: {cost}金/日[/color]\n" +
                    "[color=#ccc]人数: 24[/color]\n" +
                    "[color=#ccc]经验: 120/500[/color]\n" +
                    "[color=#ccc]士气: 正常[/color]\n" +
                    "[color=#ccc]状态: 健康[/color]";
        _detailContent.AddChild(info);
    }

    private void SetStatVal(string id, int value)
    {
        if (_stackStatLabels.TryGetValue(id, out var lbl))
            lbl.Text = value.ToString();
    }

    // ============================================================================
    // 公开接口
    // ============================================================================
    public void OpenArmy()
    {
        Visible = true;
        RefreshCharacterList();
        RefreshStackList();
        RefreshGridItems();
    }

    public void CloseArmy()
    {
        Visible = false;
        CancelDrag();
        HideTooltip();
    }

    /// <summary>绑定网格背包数据（由外部控制器调用）</summary>
    public void BindGridInventory(GridInventory inventory)
    {
        _gridInventory = inventory;
        RebuildGridCells();
        RefreshGridItems();
    }

    /// <summary>刷新背包显示（数据变化后调用）</summary>
    public void RefreshInventory()
    {
        RefreshGridItems();
    }

    /// <summary>设置队伍角色数据（由外部控制器调用）</summary>
    public void SetPartyMembers(System.Collections.Generic.List<UnitData> members)
    {
        _partyMembers = members;
        RefreshCharacterList();
    }

    // ============================================================================
    // 角色列表
    // ============================================================================
    private System.Collections.Generic.List<UnitData> _partyMembers = new();

    private void RefreshCharacterList()
    {
        // 获取横向容器
        var charRow = _characterList.GetMeta("char_row").As<HBoxContainer>();
        if (charRow == null) return;

        foreach (Node child in charRow.GetChildren())
            child.QueueFree();

        // 如果没有外部数据，显示占位示例
        if (_partyMembers.Count == 0)
        {
            AddCharacterEntry(charRow, "队长", 0, 5, true);
            AddCharacterEntry(charRow, "剑士", 1, 3, false);
            AddCharacterEntry(charRow, "法师", 2, 4, false);
            AddCharacterEntry(charRow, "弓手", 3, 2, false);
        }
        else
        {
            for (int i = 0; i < _partyMembers.Count; i++)
            {
                var unit = _partyMembers[i];
                AddCharacterEntry(charRow, unit.UnitName, i, unit.Level, i == _selectedCharIndex);
            }
        }
    }

    private void AddCharacterEntry(HBoxContainer parent, string name, int index, int level, bool selected)
    {
        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(60, 44);
        card.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        var cardBg = selected
            ? new Color(0.18f, 0.16f, 0.22f, 0.95f)
            : BgCard;
        var cardBorder = selected ? BorderHighlight : BorderDefault;

        var cardStyle = new StyleBoxFlat { BgColor = cardBg };
        cardStyle.SetBorderWidthAll(selected ? 2 : 1);
        cardStyle.BorderColor = cardBorder;
        cardStyle.SetCornerRadiusAll(4);
        cardStyle.SetContentMarginAll(3);
        card.AddThemeStyleboxOverride("panel", cardStyle);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 1);
        card.AddChild(vbox);

        // 角色名（截断）
        string displayName = name.Length > 3 ? name[..3] : name;
        var nameLbl = MakeLabel(displayName, FontSmall, selected ? TextAccent : TextPrimary);
        nameLbl.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(nameLbl);

        // 等级
        var lvlLbl = MakeLabel($"Lv.{level}", FontTiny, TextMuted);
        lvlLbl.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(lvlLbl);

        int capturedIndex = index;
        card.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _selectedCharIndex = capturedIndex;
                RefreshCharacterList();
                EmitSignal(SignalName.CharacterSelected, capturedIndex);
            }
        };

        parent.AddChild(card);
    }

    // ============================================================================
    // 堆叠列表
    // ============================================================================
    private void RefreshStackList()
    {
        foreach (Node child in _stackList.GetChildren())
            child.QueueFree();

        AddStackEntry("stack_1", "spearman", 24);
        AddStackEntry("stack_2", "archer", 18);
        AddStackEntry("stack_3", "sword_shield", 12);
        AddStackEntry("stack_4", "light_cav", 8);

        _totalArmyLabel.Text = "总兵力: 62";
        _upkeepLabel.Text = "日薪俸: 1240金";
    }

    private void AddStackEntry(string stackId, string schemeId, int count)
    {
        if (!EquipSchemes.TryGetValue(schemeId, out var scheme)) return;

        string name = scheme["name"].AsString();
        string identity = scheme["identity"].AsString();
        int cost = scheme["cost"].AsInt32();

        var entry = new PanelContainer();
        entry.CustomMinimumSize = new Vector2(0, 38);
        var eStyle = new StyleBoxFlat { BgColor = BgCard };
        eStyle.SetBorderWidthAll(1);
        eStyle.BorderColor = BorderDefault;
        eStyle.SetCornerRadiusAll(3);
        eStyle.SetContentMarginAll(Spacing2);
        entry.AddThemeStyleboxOverride("panel", eStyle);
        entry.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", Spacing3);
        entry.AddChild(hbox);

        var infoVbox = new VBoxContainer();
        infoVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        infoVbox.AddThemeConstantOverride("separation", 1);
        hbox.AddChild(infoVbox);

        infoVbox.AddChild(MakeLabel($"{name} x{count}", FontSmall, TextPrimary));
        infoVbox.AddChild(MakeLabel($"{identity} | {cost * count}金/日", FontTiny, TextMuted));

        string capStack = stackId;
        string capScheme = schemeId;
        entry.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                OnStackClicked(capStack, capScheme);
        };

        _stackList.AddChild(entry);
    }

    // ============================================================================
    // 辅助工厂
    // ============================================================================
    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
    }

    private Button MakeBtn(string text, int width, int height = 24)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = width > 0 ? new Vector2(width, height) : new Vector2(0, height);
        btn.AddThemeFontSizeOverride("font_size", FontSmall);

        var normal = new StyleBoxFlat { BgColor = new Color(0.14f, 0.13f, 0.17f) };
        normal.SetBorderWidthAll(1);
        normal.BorderColor = BorderDefault;
        normal.SetCornerRadiusAll(3);
        normal.SetContentMarginAll(3);
        btn.AddThemeStyleboxOverride("normal", normal);

        var hover = new StyleBoxFlat { BgColor = new Color(0.22f, 0.20f, 0.28f) };
        hover.SetBorderWidthAll(1);
        hover.BorderColor = BorderHighlight;
        hover.SetCornerRadiusAll(3);
        hover.SetContentMarginAll(3);
        btn.AddThemeStyleboxOverride("hover", hover);

        var pressed = new StyleBoxFlat { BgColor = new Color(0.10f, 0.09f, 0.12f) };
        pressed.SetBorderWidthAll(1);
        pressed.BorderColor = BorderHighlight;
        pressed.SetCornerRadiusAll(3);
        pressed.SetContentMarginAll(3);
        btn.AddThemeStyleboxOverride("pressed", pressed);

        btn.AddThemeColorOverride("font_color", TextPrimary);
        btn.AddThemeColorOverride("font_hover_color", TextAccent);
        return btn;
    }

    private static HSeparator MakeSep()
    {
        var sep = new HSeparator();
        sep.AddThemeStyleboxOverride("separator", MakeThinSep());
        return sep;
    }

    private static StyleBoxFlat MakeThinSep()
    {
        var s = new StyleBoxFlat();
        s.BgColor = new Color(0.3f, 0.28f, 0.25f, 0.4f);
        s.SetContentMarginAll(0);
        return s;
    }
}
