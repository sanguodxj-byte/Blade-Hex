// PartyPanel.cs
// 部队面板 — 三栏布局
// 左栏：角色切换 + 属性 + 纸娃娃装备 + 战斗属性
// 中栏：上半物品详情/商品/战利品 + 下半背包列表
// 右栏：部队编制（可收起）
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class PartyPanel : PanelContainer
{
    // ============================================================================
    // 主题
    // ============================================================================
    private static readonly Color BgPrimary = new(0.06f, 0.06f, 0.08f, 0.95f);
    private static readonly Color BgSecondary = new(0.10f, 0.10f, 0.12f, 0.85f);
    private static readonly Color BgCard = new(0.12f, 0.11f, 0.15f, 0.9f);
    private static readonly Color BgGrid = new(0.04f, 0.04f, 0.05f, 0.95f);
    private static readonly Color BgCell = new(0.08f, 0.08f, 0.10f, 0.85f);
    private static readonly Color BgCellValid = new(0.08f, 0.22f, 0.08f, 0.7f);
    private static readonly Color BgCellInvalid = new(0.28f, 0.06f, 0.06f, 0.7f);
    private static readonly Color BgItemNormal = new(0.13f, 0.12f, 0.16f, 0.95f);
    private static readonly Color BgEquipSlot = new(0.09f, 0.08f, 0.11f, 0.9f);
    private static readonly Color BorderDefault = new(0.3f, 0.3f, 0.35f, 0.6f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color BorderDrag = new(0.85f, 0.72f, 0.25f, 0.95f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPositive = new(0.3f, 0.85f, 0.3f);
    private static readonly Color TextNegative = new(0.9f, 0.3f, 0.25f);

    private const int EquipSlotSize = 54;
    private const int CellSize = 42;
    private const int CellGap = 2;

    // 右栏折叠状态
    private bool _armyPanelVisible = true;

    // ============================================================================
    // 数据
    // ============================================================================
    private PartyRoster? _roster;
    private PartyInventory? _inventory;
    private int _currentIndex = 0;

    private UnitData? SelectedUnit => _roster != null && _currentIndex >= 0 && _currentIndex < _roster.Members.Count
        ? _roster.Members[_currentIndex] : null;

    // ============================================================================
    // UI 引用
    // ============================================================================
    private VBoxContainer _leftCol = null!;
    private VBoxContainer _centerCol = null!;
    private VBoxContainer _detailArea = null!;     // 中栏上半：物品详情/商品/战利品
    private VBoxContainer _rightCol = null!;
    private PanelContainer _rightPanel = null!;    // 右栏面板（可折叠）
    private Label _nameLabel = null!;
    private Label _indexLabel = null!;
    private Button _toggleArmyBtn = null!;
    private readonly Dictionary<string, PanelContainer> _equipSlots = new();

    // ============================================================================
    // 生命周期
    // ============================================================================

    public override void _Ready()
    {
        _BuildUi();
    }

    // ============================================================================
    // UI 构建
    // ============================================================================

    private void _BuildUi()
    {
        // 面板居中显示（占屏幕大部分）
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AnchorLeft = 0.08f;
        AnchorRight = 0.92f;
        AnchorTop = 0.04f;
        AnchorBottom = 0.96f;
        OffsetLeft = 0; OffsetRight = 0; OffsetTop = 0; OffsetBottom = 0;

        var style = new StyleBoxFlat { BgColor = BgPrimary };
        style.SetBorderWidthAll(2);
        style.BorderColor = BorderHighlight;
        style.SetCornerRadiusAll(6);
        AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 8);
        mainVbox.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddChild(mainVbox);

        // ─── 顶部标题栏 ───
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        mainVbox.AddChild(header);

        var title = _MakeLabel("部队管理", 20, TextAccent);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        // 军队栏折叠按钮
        _toggleArmyBtn = new Button { Text = "军队 ▶", CustomMinimumSize = new Vector2(80, 30) };
        _toggleArmyBtn.AddThemeFontSizeOverride("font_size", 12);
        _toggleArmyBtn.Pressed += _ToggleArmyPanel;
        header.AddChild(_toggleArmyBtn);

        var closeBtn = new Button { Text = "✕", CustomMinimumSize = new Vector2(36, 30) };
        closeBtn.Pressed += () => Visible = false;
        header.AddChild(closeBtn);

        mainVbox.AddChild(new HSeparator());

        // ─── 主体：左 | 中 | 右(可折叠) ───
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 12);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        mainVbox.AddChild(columns);

        // ── 左栏：角色切换 + 属性 + 纸娃娃装备 ──
        var leftPanel = _MakeColumnPanel(260);
        columns.AddChild(leftPanel);
        var leftScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        leftScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        leftPanel.AddChild(leftScroll);
        _leftCol = new VBoxContainer();
        _leftCol.AddThemeConstantOverride("separation", 6);
        _leftCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leftScroll.AddChild(_leftCol);

        // ── 中栏：上半详情区 + 下半背包 ──
        var centerPanel = _MakeColumnPanel(0);
        centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        columns.AddChild(centerPanel);
        _centerCol = new VBoxContainer();
        _centerCol.AddThemeConstantOverride("separation", 6);
        _centerCol.SizeFlagsVertical = SizeFlags.ExpandFill;
        _centerCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        centerPanel.AddChild(_centerCol);

        // ── 右栏：军队列表（可折叠） ──
        _rightPanel = _MakeColumnPanel(240);
        columns.AddChild(_rightPanel);
        var rightScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rightScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        _rightPanel.AddChild(rightScroll);
        _rightCol = new VBoxContainer();
        _rightCol.AddThemeConstantOverride("separation", 4);
        _rightCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rightScroll.AddChild(_rightCol);
    }

    private void _ToggleArmyPanel()
    {
        _armyPanelVisible = !_armyPanelVisible;
        _rightPanel.Visible = _armyPanelVisible;
        _toggleArmyBtn.Text = _armyPanelVisible ? "军队 ▶" : "◀ 军队";
    }

    private PanelContainer _MakeColumnPanel(int minWidth)
    {
        var panel = new PanelContainer();
        if (minWidth > 0) panel.CustomMinimumSize = new Vector2(minWidth, 0);
        var s = new StyleBoxFlat { BgColor = BgSecondary };
        s.SetCornerRadiusAll(6);
        s.SetContentMarginAll(12);
        panel.AddThemeStyleboxOverride("panel", s);
        return panel;
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    public void Open(PartyRoster roster, PartyInventory inventory)
    {
        _roster = roster;
        _inventory = inventory;
        _currentIndex = 0;
        Visible = true;
        Refresh();
    }

    public void OpenTab(string tabName, UnitData? unitData = null)
    {
        Visible = true;
        Refresh();
    }

    public void Refresh()
    {
        _RefreshLeft();
        _RefreshCenter();
        _RefreshRight();
    }

    public void RefreshUi()
    {
        if (!IsInsideTree() || !Visible) return;
        Refresh();
    }

    // ============================================================================
    // 左列：角色切换 + 属性 + 纸娃娃装备
    // ============================================================================

    private void _RefreshLeft()
    {
        foreach (Node c in _leftCol.GetChildren()) c.QueueFree();
        _equipSlots.Clear();

        // ─── 角色切换 ───
        var switchRow = new HBoxContainer();
        switchRow.Alignment = BoxContainer.AlignmentMode.Center;
        switchRow.AddThemeConstantOverride("separation", 8);
        _leftCol.AddChild(switchRow);

        var prevBtn = new Button { Text = "◀", CustomMinimumSize = new Vector2(30, 30) };
        prevBtn.Pressed += () => { _SwitchChar(-1); };
        switchRow.AddChild(prevBtn);

        int total = _roster?.Members.Count ?? 0;
        _nameLabel = _MakeLabel(SelectedUnit?.UnitName ?? "无角色", 16, TextAccent);
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        switchRow.AddChild(_nameLabel);

        var nextBtn = new Button { Text = "▶", CustomMinimumSize = new Vector2(30, 30) };
        nextBtn.Pressed += () => { _SwitchChar(1); };
        switchRow.AddChild(nextBtn);

        _indexLabel = _MakeLabel($"({_currentIndex + 1}/{total})", 11, TextMuted);
        _indexLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _leftCol.AddChild(_indexLabel);

        if (SelectedUnit == null) return;
        var u = SelectedUnit;

        // ─── 角色立绘（统一渲染组件，与战斗 / 大地图共享） ───
        var avatar = new BladeHex.View.Unit.CharacterAvatarControl
        {
            Mode = BladeHex.View.Unit.CharacterAvatarControl.DisplayMode.Full,
            CustomMinimumSize = new Vector2(180, 220),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        _leftCol.AddChild(avatar);
        avatar.SetUnit(u);

        // ─── HP/MP ───
        int hp = _roster != null ? PartyRoster.GetCurrentHp(u) : u.BaseMaxHp;
        int maxHp = u.BaseMaxHp + (int)Math.Floor(Math.Sqrt(u.Con / 4.0)) * u.Level;
        _leftCol.AddChild(_MakeBar("生命", hp, maxHp, TextPositive));
        int maxMana = Math.Max(1, u.Intel / 2 + u.Level);
        _leftCol.AddChild(_MakeBar("法力", u.CurrentMana, maxMana, new Color(0.3f, 0.5f, 1.0f)));

        // ─── 六维属性 ───
        _leftCol.AddChild(_MakeLabel($"Lv.{u.Level}  力量 {u.Str}  敏捷 {u.Dex}  体质 {u.Con}", 11, TextSecondary));
        _leftCol.AddChild(_MakeLabel($"       智力 {u.Intel}  感知 {u.Wis}  魅力 {u.Cha}", 11, TextSecondary));

        _leftCol.AddChild(new HSeparator());

        // ─── 纸娃娃装备（纯图标，无文字无emoji） ───
        _BuildPaperdoll(u);
    }

    /// <summary>纸娃娃装备布局（纯图标格子，无文字）</summary>
    private void _BuildPaperdoll(UnitData u)
    {
        var equipBox = new VBoxContainer();
        equipBox.AddThemeConstantOverride("separation", 3);
        _leftCol.AddChild(equipBox);

        // 行1: 头盔
        var row1 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        equipBox.AddChild(row1);
        row1.AddChild(_MakeEquipIcon("helmet", "头盔", u.Helmet));

        // 行2: 主手 | 铠甲 | 副手
        var row2 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row2.AddThemeConstantOverride("separation", 4);
        equipBox.AddChild(row2);
        row2.AddChild(_MakeEquipIcon("primary_main", "主手", u.PrimaryMainHand));
        row2.AddChild(_MakeEquipIcon("armor", "铠甲", u.Armor));
        row2.AddChild(_MakeEquipIcon("primary_off", "副手", u.PrimaryOffHand));

        // 行3: 护手 | 空 | 饰品1
        var row3 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row3.AddThemeConstantOverride("separation", 4);
        equipBox.AddChild(row3);
        row3.AddChild(_MakeEquipIcon("gauntlets", "护手", u.Gauntlets));
        row3.AddChild(new Control { CustomMinimumSize = new Vector2(EquipSlotSize, 0) });
        row3.AddChild(_MakeEquipIcon("accessory_1", "饰品", u.Accessory1));

        // 行4: 鞋子
        var row4 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        equipBox.AddChild(row4);
        row4.AddChild(_MakeEquipIcon("boots", "鞋子", u.Boots));

        // 行5: 副武器 | 饰品2 | 坐骑
        var row5 = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row5.AddThemeConstantOverride("separation", 4);
        equipBox.AddChild(row5);
        row5.AddChild(_MakeEquipIcon("secondary_main", "副武器", u.SecondaryMainHand));
        row5.AddChild(_MakeEquipIcon("accessory_2", "饰品", u.Accessory2));
        row5.AddChild(_MakeEquipIconMount("mount", "坐骑", u.Mount));

        _leftCol.AddChild(new HSeparator());

        // ─── 战斗属性 ───
        var statGrid = new GridContainer { Columns = 4 };
        statGrid.AddThemeConstantOverride("h_separation", 6);
        statGrid.AddThemeConstantOverride("v_separation", 3);
        _leftCol.AddChild(statGrid);

        // 护甲值 = 10 + DEX修正(受甲限) + sqrt(DR)
        int dexMod = (int)Math.Floor(Math.Sqrt(u.Dex / 2.0));
        if (u.Armor != null && u.Armor.MaxDexBonus < 99)
            dexMod = Math.Min(dexMod, u.Armor.MaxDexBonus);
        int drTotal = u.Armor?.DrThreshold ?? 0;
        int ac = 10 + dexMod + (int)Math.Floor(Math.Sqrt(drTotal));
        _AddStatPair(statGrid, "闪避", ac.ToString());

        // 行动力 = 12 + DEX修正 + CON修正/2 - 护甲惩罚
        int conMod = (int)Math.Floor(Math.Sqrt(u.Con / 2.0));
        int apPenalty = u.Armor?.ApPenalty ?? 0;
        int maxAp = 12 + (int)Math.Floor(Math.Sqrt(u.Dex / 2.0)) + conMod / 2 - apPenalty;
        _AddStatPair(statGrid, "行动力", $"{maxAp}");

        // 伤害显示为范围
        int dmgMin = 1, dmgMax = 3;
        if (u.PrimaryMainHand != null)
        {
            dmgMin = u.PrimaryMainHand.DamageDiceCount;
            dmgMax = u.PrimaryMainHand.DamageDiceCount * u.PrimaryMainHand.DamageDiceSides;
        }
        _AddStatPair(statGrid, "伤害", $"{dmgMin}-{dmgMax}");

        // 暴击率 = WISCritTier决定
        int wisCritTier = (int)Math.Floor(Math.Sqrt(Math.Max(0, u.Wis - 14) / 4.0));
        int critPct = 5 + wisCritTier * 5;
        _AddStatPair(statGrid, "暴击", $"{critPct}%");
    }

    private void _SwitchChar(int dir)
    {
        if (_roster == null || _roster.Members.Count == 0) return;
        _currentIndex = (_currentIndex + dir + _roster.Members.Count) % _roster.Members.Count;
        Refresh();
    }

    // ============================================================================
    // 中栏：上半物品详情/商品/战利品 + 下半网格背包
    // ============================================================================

    private GridInventory? _gridInventory;
    private Control _gridArea = null!;
    private readonly Dictionary<Vector2I, Panel> _cellPanels = new();
    private readonly Dictionary<string, Control> _itemControls = new();

    // 拖拽
    private GridItem? _draggedItem;
    private Control? _dragGhost;
    private Vector2 _dragOffset;
    private bool _isDragging;
    private int _hoverCellX = -1;
    private int _hoverCellY = -1;

    private void _RefreshCenter()
    {
        foreach (Node c in _centerCol.GetChildren()) c.QueueFree();

        // ─── 上半：详情区域 ───
        var detailPanel = new PanelContainer();
        var dStyle = new StyleBoxFlat { BgColor = new Color(0.07f, 0.07f, 0.09f, 0.8f) };
        dStyle.SetBorderWidthAll(1);
        dStyle.BorderColor = BorderDefault;
        dStyle.SetCornerRadiusAll(4);
        dStyle.SetContentMarginAll(8);
        detailPanel.AddThemeStyleboxOverride("panel", dStyle);
        detailPanel.CustomMinimumSize = new Vector2(0, 100);
        _centerCol.AddChild(detailPanel);

        var detailScroll = new ScrollContainer();
        detailScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        detailScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        detailPanel.AddChild(detailScroll);

        _detailArea = new VBoxContainer();
        _detailArea.AddThemeConstantOverride("separation", 4);
        _detailArea.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detailScroll.AddChild(_detailArea);

        _detailArea.AddChild(_MakeLabel("点击物品查看详情", 12, TextMuted));

        _centerCol.AddChild(new HSeparator());

        // ─── 下半：网格背包 ───
        var invHeader = new HBoxContainer();
        invHeader.AddThemeConstantOverride("separation", 6);
        _centerCol.AddChild(invHeader);
        invHeader.AddChild(_MakeLabel("背包", 13, TextAccent));

        var capLbl = _MakeLabel("", 10, TextSecondary);
        capLbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        invHeader.AddChild(capLbl);

        var sortBtn = new Button { Text = "整理", CustomMinimumSize = new Vector2(48, 22) };
        sortBtn.AddThemeFontSizeOverride("font_size", 11);
        sortBtn.Pressed += () => { _gridInventory?.AutoSort(); _RefreshGrid(); };
        invHeader.AddChild(sortBtn);

        // 网格外框
        var gridOuter = new PanelContainer();
        var outerStyle = new StyleBoxFlat { BgColor = BgGrid };
        outerStyle.SetBorderWidthAll(1);
        outerStyle.BorderColor = new Color(0.2f, 0.18f, 0.16f, 0.8f);
        outerStyle.SetCornerRadiusAll(4);
        outerStyle.SetContentMarginAll(4);
        gridOuter.AddThemeStyleboxOverride("panel", outerStyle);
        gridOuter.SizeFlagsVertical = SizeFlags.ExpandFill;
        _centerCol.AddChild(gridOuter);

        // 滚动容器
        var gridScroll = new ScrollContainer();
        gridScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        gridScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        gridScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        gridOuter.AddChild(gridScroll);

        // 网格绘制区
        _gridArea = new Control();
        _gridArea.MouseFilter = MouseFilterEnum.Stop;
        _gridArea.GuiInput += _OnGridInput;
        _gridArea.MouseExited += () => { _hoverCellX = -1; _hoverCellY = -1; _ClearHighlights(); };
        gridScroll.AddChild(_gridArea);

        // 初始化网格背包（如果还没有）
        _EnsureGridInventory();
        _BuildGridCells();
        _RefreshGrid();

        // 更新容量标签
        if (_gridInventory != null)
            capLbl.Text = $"{_gridInventory.UsedCells}/{_gridInventory.TotalCells}";
    }

    private void _EnsureGridInventory()
    {
        if (_gridInventory != null) return;
        _gridInventory = new GridInventory();

        // 根据队伍属性计算容量
        if (_roster != null && _roster.Members.Count > 0)
            _gridInventory.RecalculateCapacity(_roster.Members);

        // 将旧背包物品迁移到网格背包
        if (_inventory != null)
        {
            foreach (var slot in _inventory.Slots)
            {
                // 创建临时ItemData用于放置
                var tempItem = new ItemData
                {
                    ItemName = slot.ItemName,
                    ItemId = slot.ItemName,
                    Price = slot.Value,
                    Description = slot.Description,
                };
                ItemSizeConfig.ApplyRecommendedSize(tempItem);
                _gridInventory.TryAutoPlace(tempItem, slot.Quantity);
            }
        }
    }

    private void _BuildGridCells()
    {
        foreach (var cell in _cellPanels.Values) cell.QueueFree();
        _cellPanels.Clear();

        if (_gridInventory == null) return;

        int gw = _gridInventory.GridWidth;
        int gh = _gridInventory.GridHeight;
        int totalW = gw * (CellSize + CellGap) - CellGap;
        int totalH = gh * (CellSize + CellGap) - CellGap;
        _gridArea.CustomMinimumSize = new Vector2(totalW, totalH);

        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                var cell = new Panel();
                cell.Position = new Vector2(x * (CellSize + CellGap), y * (CellSize + CellGap));
                cell.Size = new Vector2(CellSize, CellSize);
                cell.MouseFilter = MouseFilterEnum.Ignore;

                bool alt = (x + y) % 2 == 1;
                var bg = alt ? new Color(BgCell.R + 0.012f, BgCell.G + 0.012f, BgCell.B + 0.015f, BgCell.A) : BgCell;
                var cs = new StyleBoxFlat { BgColor = bg };
                cs.SetBorderWidthAll(1);
                cs.BorderColor = new Color(0.18f, 0.18f, 0.2f, 0.4f);
                cs.SetCornerRadiusAll(1);
                cell.AddThemeStyleboxOverride("panel", cs);

                _gridArea.AddChild(cell);
                _cellPanels[new Vector2I(x, y)] = cell;
            }
        }
    }

    private void _RefreshGrid()
    {
        foreach (var ctrl in _itemControls.Values) ctrl.QueueFree();
        _itemControls.Clear();

        if (_gridInventory == null) return;

        foreach (var gi in _gridInventory.Items)
            _CreateItemWidget(gi);
    }

    private void _CreateItemWidget(GridItem gi)
    {
        var w = new Panel();
        int px = gi.GridX * (CellSize + CellGap);
        int py = gi.GridY * (CellSize + CellGap);
        int pw = gi.Width * (CellSize + CellGap) - CellGap;
        int ph = gi.Height * (CellSize + CellGap) - CellGap;
        w.Position = new Vector2(px, py);
        w.Size = new Vector2(pw, ph);
        w.MouseFilter = MouseFilterEnum.Stop;
        w.ZIndex = 1;
        w.MouseDefaultCursorShape = CursorShape.PointingHand;

        var rc = gi.Item.GetRarityColor();
        var bg = new Color(BgItemNormal.R + rc.R * 0.03f, BgItemNormal.G + rc.G * 0.03f, BgItemNormal.B + rc.B * 0.03f, 0.95f);
        var ws = new StyleBoxFlat { BgColor = bg };
        ws.SetBorderWidthAll(1);
        ws.BorderColor = new Color(rc.R * 0.6f, rc.G * 0.6f, rc.B * 0.6f, 0.8f);
        ws.SetCornerRadiusAll(2);
        w.AddThemeStyleboxOverride("panel", ws);

        // 图标
        var icon = new TextureRect();
        icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        icon.OffsetLeft = 3; icon.OffsetTop = 3; icon.OffsetRight = -3; icon.OffsetBottom = -12;
        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.MouseFilter = MouseFilterEnum.Ignore;
        if (!string.IsNullOrEmpty(gi.Item.IconId))
        {
            var tex = GD.Load<Texture2D>(gi.Item.IconId);
            if (tex != null) icon.Texture = tex;
        }
        w.AddChild(icon);

        // 名称
        int maxC = gi.Width * 3;
        var nm = gi.Item.ItemName.Length > maxC ? gi.Item.ItemName[..maxC] : gi.Item.ItemName;
        var nl = new Label { Text = nm };
        nl.AddThemeFontSizeOverride("font_size", 9);
        nl.AddThemeColorOverride("font_color", rc);
        nl.HorizontalAlignment = HorizontalAlignment.Center;
        nl.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        nl.OffsetTop = -11; nl.OffsetBottom = 0;
        nl.MouseFilter = MouseFilterEnum.Ignore;
        w.AddChild(nl);

        // 数量
        if (gi.Quantity > 1)
        {
            var ql = new Label { Text = $"×{gi.Quantity}" };
            ql.AddThemeFontSizeOverride("font_size", 10);
            ql.AddThemeColorOverride("font_color", TextPrimary);
            ql.HorizontalAlignment = HorizontalAlignment.Right;
            ql.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
            ql.OffsetRight = -2; ql.OffsetTop = 1;
            ql.MouseFilter = MouseFilterEnum.Ignore;
            w.AddChild(ql);
        }

        w.TooltipText = $"{gi.Item.GetFullName()}\n{gi.Item.GetRarityName()} · {gi.Item.InvWidth}×{gi.Item.InvHeight}格\n价值: {gi.Item.GetSellPrice()}金";

        // 拖拽 + 点击
        w.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                {
                    _StartDrag(gi, w, mb.GlobalPosition);
                    GetViewport().SetInputAsHandled();
                }
                else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
                {
                    _ShowItemDetail(gi);
                    GetViewport().SetInputAsHandled();
                }
            }
        };

        _gridArea.AddChild(w);
        _itemControls[gi.InstanceId] = w;
    }

    private void _ShowItemDetail(GridItem gi)
    {
        if (_detailArea == null) return;
        foreach (Node c in _detailArea.GetChildren()) c.QueueFree();

        var item = gi.Item;
        _detailArea.AddChild(_MakeLabel(item.GetFullName(), 14, item.GetRarityColor()));
        _detailArea.AddChild(_MakeLabel(item.GetRarityName(), 11, TextMuted));
        if (!string.IsNullOrEmpty(item.Description))
            _detailArea.AddChild(_MakeLabel(item.Description, 12, TextPrimary));
        _detailArea.AddChild(_MakeLabel($"占用 {item.InvWidth}×{item.InvHeight} 格 · 价值 {item.GetSellPrice()} 金", 11, TextSecondary));

        if (item is WeaponData wpn)
            _detailArea.AddChild(_MakeLabel($"伤害 {wpn.DamageDiceCount}-{wpn.DamageDiceCount * wpn.DamageDiceSides}", 12, TextPrimary));
        else if (item is ArmorData arm)
            _detailArea.AddChild(_MakeLabel($"装甲 {arm.DrThreshold} · 闪避上限 {(arm.MaxDexBonus < 99 ? arm.MaxDexBonus.ToString() : "无限")}", 12, TextPrimary));

        string affixes = item.GetAffixDescriptions();
        if (!string.IsNullOrEmpty(affixes))
            _detailArea.AddChild(_MakeLabel(affixes, 11, new Color(0.7f, 0.5f, 0.9f)));
    }

    // ─── 拖拽 ───

    private void _StartDrag(GridItem gi, Control w, Vector2 mousePos)
    {
        _isDragging = true;
        _draggedItem = gi;
        _dragOffset = w.GlobalPosition - mousePos;

        _dragGhost = new Panel { Size = w.Size, ZIndex = 80, MouseFilter = MouseFilterEnum.Ignore };
        _dragGhost.Modulate = new Color(1, 1, 1, 0.7f);
        var gs = new StyleBoxFlat { BgColor = new Color(0.15f, 0.12f, 0.2f, 0.85f) };
        gs.SetBorderWidthAll(2); gs.BorderColor = BorderDrag; gs.SetCornerRadiusAll(3);
        _dragGhost.AddThemeStyleboxOverride("panel", gs);
        var gl = new Label { Text = gi.Item.ItemName };
        gl.AddThemeFontSizeOverride("font_size", 11);
        gl.AddThemeColorOverride("font_color", gi.Item.GetRarityColor());
        gl.HorizontalAlignment = HorizontalAlignment.Center;
        gl.VerticalAlignment = VerticalAlignment.Center;
        gl.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        gl.MouseFilter = MouseFilterEnum.Ignore;
        _dragGhost.AddChild(gl);
        AddChild(_dragGhost);
        _dragGhost.GlobalPosition = mousePos + _dragOffset;

        w.Modulate = new Color(1, 1, 1, 0.25f);
    }

    private void _CancelDrag()
    {
        if (!_isDragging) return;
        _isDragging = false;
        if (_dragGhost != null) { _dragGhost.QueueFree(); _dragGhost = null; }
        if (_draggedItem != null && _itemControls.TryGetValue(_draggedItem.InstanceId, out var c))
            c.Modulate = Colors.White;
        _draggedItem = null;
        _ClearHighlights();
    }

    private void _CompleteDrag(int tx, int ty)
    {
        if (_gridInventory == null || _draggedItem == null) return;
        var target = _gridInventory.GetItemAt(tx, ty);
        if (target != null && target != _draggedItem)
            _gridInventory.TrySwap(_draggedItem, target);
        else
            _gridInventory.TryMove(_draggedItem, tx, ty);
        _CancelDrag();
        _RefreshGrid();
    }

    private void _OnGridInput(InputEvent ev)
    {
        if (ev is InputEventMouseMotion mm)
        {
            var cell = _PosToCell(mm.Position);
            _hoverCellX = cell.X; _hoverCellY = cell.Y;
            if (_isDragging && _dragGhost != null)
            {
                _dragGhost.GlobalPosition = mm.GlobalPosition + _dragOffset;
                _UpdateHighlights();
            }
        }
        else if (ev is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left && !mb.Pressed && _isDragging)
            {
                var cell = _PosToCell(mb.Position);
                if (cell.X >= 0) _CompleteDrag(cell.X, cell.Y);
                else _CancelDrag();
            }
        }
    }

    public override void _Input(InputEvent ev)
    {
        if (!Visible || !_isDragging) return;
        if (ev is InputEventMouseMotion mm && _dragGhost != null)
            _dragGhost.GlobalPosition = mm.GlobalPosition + _dragOffset;
        else if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
            _CancelDrag();
        else if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
            _CancelDrag();
    }

    private void _UpdateHighlights()
    {
        _ClearHighlights();
        if (_gridInventory == null || _draggedItem == null || _hoverCellX < 0) return;
        bool ok = _gridInventory.CanPlaceSize(_draggedItem.Width, _draggedItem.Height, _hoverCellX, _hoverCellY, _draggedItem);
        var hlBg = ok ? BgCellValid : BgCellInvalid;
        var hlBd = ok ? new Color(0.25f, 0.7f, 0.25f, 0.8f) : new Color(0.7f, 0.2f, 0.2f, 0.8f);
        for (int dx = 0; dx < _draggedItem.Width; dx++)
            for (int dy = 0; dy < _draggedItem.Height; dy++)
            {
                var k = new Vector2I(_hoverCellX + dx, _hoverCellY + dy);
                if (_cellPanels.TryGetValue(k, out var p))
                {
                    var s = new StyleBoxFlat { BgColor = hlBg };
                    s.SetBorderWidthAll(1); s.BorderColor = hlBd; s.SetCornerRadiusAll(1);
                    p.AddThemeStyleboxOverride("panel", s);
                }
            }
    }

    private void _ClearHighlights()
    {
        foreach (var kvp in _cellPanels)
        {
            bool alt = (kvp.Key.X + kvp.Key.Y) % 2 == 1;
            var bg = alt ? new Color(BgCell.R + 0.012f, BgCell.G + 0.012f, BgCell.B + 0.015f, BgCell.A) : BgCell;
            var s = new StyleBoxFlat { BgColor = bg };
            s.SetBorderWidthAll(1); s.BorderColor = new Color(0.18f, 0.18f, 0.2f, 0.4f); s.SetCornerRadiusAll(1);
            kvp.Value.AddThemeStyleboxOverride("panel", s);
        }
    }

    private Vector2I _PosToCell(Vector2 pos)
    {
        int x = (int)(pos.X / (CellSize + CellGap));
        int y = (int)(pos.Y / (CellSize + CellGap));
        if (_gridInventory == null || x < 0 || x >= _gridInventory.GridWidth || y < 0 || y >= _gridInventory.GridHeight)
            return new Vector2I(-1, -1);
        return new Vector2I(x, y);
    }

    /// <summary>显示物品详情到中栏上半区域</summary>
    public void ShowDetail(string title, string description)
    {
        if (_detailArea == null) return;
        foreach (Node c in _detailArea.GetChildren()) c.QueueFree();
        _detailArea.AddChild(_MakeLabel(title, 14, TextAccent));
        if (!string.IsNullOrEmpty(description))
            _detailArea.AddChild(_MakeLabel(description, 12, TextPrimary));
    }

    /// <summary>显示商品列表到详情区域（市场使用）</summary>
    public void ShowTradeGoods(List<InventorySlot> goods)
    {
        if (_detailArea == null) return;
        foreach (Node c in _detailArea.GetChildren()) c.QueueFree();
        _detailArea.AddChild(_MakeLabel("商品", 14, TextAccent));
        foreach (var g in goods)
            _detailArea.AddChild(_MakeLabel($"  {g.ItemName} — {g.Value}金", 12, TextPrimary));
    }

    /// <summary>显示战利品到详情区域</summary>
    public void ShowLoot(List<LootEntry> loot)
    {
        if (_detailArea == null) return;
        foreach (Node c in _detailArea.GetChildren()) c.QueueFree();
        _detailArea.AddChild(_MakeLabel("战利品", 14, TextAccent));
        foreach (var l in loot)
            _detailArea.AddChild(_MakeLabel($"  {l.ItemName} ×{l.Quantity}", 12, TextPrimary));
    }

    private void _OnItemClicked(int slotIndex, InventorySlot slot)
    {
        if (SelectedUnit == null || _inventory == null) return;
        if (slot.ItemType == LootEntry.LootType.Weapon)
        { _inventory.EquipWeapon(slotIndex, SelectedUnit); Refresh(); }
        else if (slot.ItemType == LootEntry.LootType.Armor)
        { _inventory.EquipArmor(slotIndex, SelectedUnit); Refresh(); }
    }

    // ============================================================================
    // 右栏：军队列表（可收起）
    // ============================================================================

    private void _RefreshRight()
    {
        foreach (Node c in _rightCol.GetChildren()) c.QueueFree();

        _rightCol.AddChild(_MakeLabel("部队编制", 14, TextAccent));
        _rightCol.AddChild(new HSeparator());

        // 模拟部队数据（实际应从外部传入）
        _AddTroopEntry("枪兵", 24, "反骑步兵", 72);
        _AddTroopEntry("弓手", 18, "远程步兵", 65);
        _AddTroopEntry("剑盾兵", 12, "防御步兵", 80);
        _AddTroopEntry("轻骑", 8, "骑兵", 58);

        _rightCol.AddChild(new HSeparator());
        _rightCol.AddChild(_MakeLabel($"总兵力: 62", 12, TextAccent));
        _rightCol.AddChild(_MakeLabel($"日薪俸: 1240金", 11, TextSecondary));
    }

    private void _AddTroopEntry(string name, int count, string role, int morale)
    {
        var entry = new VBoxContainer();
        entry.AddThemeConstantOverride("separation", 1);
        _rightCol.AddChild(entry);

        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 4);
        entry.AddChild(topRow);

        topRow.AddChild(_MakeLabel($"{name} ×{count}", 12, TextPrimary));
        var roleLbl = _MakeLabel(role, 10, TextMuted);
        roleLbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        roleLbl.HorizontalAlignment = HorizontalAlignment.Right;
        topRow.AddChild(roleLbl);

        // 士气条
        var moraleColor = morale >= 70 ? TextPositive : morale >= 40 ? TextAccent : TextNegative;
        var moraleRow = new HBoxContainer();
        moraleRow.AddThemeConstantOverride("separation", 4);
        entry.AddChild(moraleRow);
        moraleRow.AddChild(_MakeLabel("士气", 9, TextMuted));

        var bar = new ProgressBar();
        bar.MinValue = 0;
        bar.MaxValue = 100;
        bar.Value = morale;
        bar.ShowPercentage = false;
        bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bar.CustomMinimumSize = new Vector2(80, 10);
        var fill = new StyleBoxFlat { BgColor = moraleColor };
        fill.SetCornerRadiusAll(2);
        bar.AddThemeStyleboxOverride("fill", fill);
        moraleRow.AddChild(bar);

        moraleRow.AddChild(_MakeLabel($"{morale}%", 9, moraleColor));
    }

    // ============================================================================
    // 装备槽位创建（纯图标，无文字无emoji）
    // ============================================================================

    /// <summary>创建装备图标槽位（纯色块+图标纹理，无文字）</summary>
    private PanelContainer _MakeEquipIcon(string slotId, string label, ItemData? equipped)
    {
        var slot = new PanelContainer();
        slot.CustomMinimumSize = new Vector2(EquipSlotSize, EquipSlotSize);
        slot.TooltipText = equipped != null ? $"{label}: {equipped.GetFullName()}" : $"{label}: 空";
        slot.MouseDefaultCursorShape = CursorShape.PointingHand;

        var bgColor = equipped != null
            ? new Color(0.1f, 0.09f, 0.13f, 0.95f)
            : BgEquipSlot;
        var borderColor = equipped != null
            ? new Color(equipped.GetRarityColor().R * 0.7f, equipped.GetRarityColor().G * 0.7f, equipped.GetRarityColor().B * 0.7f, 0.85f)
            : new Color(0.28f, 0.26f, 0.3f, 0.7f);

        var style = new StyleBoxFlat { BgColor = bgColor };
        style.SetBorderWidthAll(equipped != null ? 2 : 1);
        style.BorderColor = borderColor;
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(2);
        slot.AddThemeStyleboxOverride("panel", style);

        // 图标纹理
        var iconRect = new TextureRect();
        iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        iconRect.OffsetLeft = 4; iconRect.OffsetTop = 4;
        iconRect.OffsetRight = -4; iconRect.OffsetBottom = -4;
        iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        iconRect.MouseFilter = MouseFilterEnum.Ignore;
        if (equipped != null && !string.IsNullOrEmpty(equipped.IconId))
        {
            var tex = GD.Load<Texture2D>(equipped.IconId);
            if (tex != null) iconRect.Texture = tex;
        }
        slot.AddChild(iconRect);

        // 右键卸下
        slot.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && equipped != null)
                _UnequipSlot(slotId);
        };

        _equipSlots[slotId] = slot;
        return slot;
    }

    /// <summary>坐骑槽位（MountData不继承ItemData）</summary>
    private PanelContainer _MakeEquipIconMount(string slotId, string label, MountData? mount)
    {
        var slot = new PanelContainer();
        slot.CustomMinimumSize = new Vector2(EquipSlotSize, EquipSlotSize);
        slot.TooltipText = mount != null ? $"{label}: {mount.MountName}" : $"{label}: 空";
        slot.MouseDefaultCursorShape = CursorShape.PointingHand;

        var bgColor = mount != null ? new Color(0.1f, 0.09f, 0.13f, 0.95f) : BgEquipSlot;
        var borderColor = mount != null
            ? new Color(0.5f, 0.4f, 0.2f, 0.85f)
            : new Color(0.28f, 0.26f, 0.3f, 0.7f);

        var style = new StyleBoxFlat { BgColor = bgColor };
        style.SetBorderWidthAll(mount != null ? 2 : 1);
        style.BorderColor = borderColor;
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(2);
        slot.AddThemeStyleboxOverride("panel", style);

        // 图标纹理（坐骑暂无图标，留空）
        var iconRect = new TextureRect();
        iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        iconRect.OffsetLeft = 4; iconRect.OffsetTop = 4;
        iconRect.OffsetRight = -4; iconRect.OffsetBottom = -4;
        iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        iconRect.MouseFilter = MouseFilterEnum.Ignore;
        slot.AddChild(iconRect);

        _equipSlots[slotId] = slot;
        return slot;
    }

    /// <summary>卸下装备放回背包</summary>
    private void _UnequipSlot(string slotId)
    {
        if (SelectedUnit == null || _inventory == null) return;
        var removed = SelectedUnit.UnequipItem(slotId);
        if (removed != null)
        {
            var entry = new LootEntry(removed.ItemName,
                removed is WeaponData ? LootEntry.LootType.Weapon : LootEntry.LootType.Armor,
                1, removed.Price, removed.Description);
            _inventory.Add(entry);
        }
        Refresh();
    }

    private void _AddStatPair(GridContainer grid, string name, string value)
    {
        var n = new Label { Text = name };
        n.AddThemeFontSizeOverride("font_size", 13);
        n.AddThemeColorOverride("font_color", TextSecondary);
        grid.AddChild(n);

        var v = new Label { Text = value };
        v.AddThemeFontSizeOverride("font_size", 13);
        grid.AddChild(v);
    }

    // ============================================================================
    // UI 工具
    // ============================================================================

    private static Label _MakeLabel(string text, int fontSize, Color color)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.AddThemeColorOverride("font_color", color);
        return lbl;
    }

    private static HBoxContainer _MakeBar(string label, int current, int max, Color barColor)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);

        var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(28, 0) };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        hbox.AddChild(lbl);

        var bar = new ProgressBar();
        bar.MinValue = 0;
        bar.MaxValue = Math.Max(max, 1);
        bar.Value = current;
        bar.ShowPercentage = false;
        bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bar.CustomMinimumSize = new Vector2(120, 16);
        var fill = new StyleBoxFlat { BgColor = barColor };
        fill.SetCornerRadiusAll(3);
        bar.AddThemeStyleboxOverride("fill", fill);
        hbox.AddChild(bar);

        var val = new Label { Text = $"{current}/{max}" };
        val.AddThemeFontSizeOverride("font_size", 11);
        val.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        hbox.AddChild(val);

        return hbox;
    }
}
