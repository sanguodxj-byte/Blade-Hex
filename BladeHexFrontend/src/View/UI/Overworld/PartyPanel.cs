// PartyPanel.cs
// 部队面板 — 三栏布局协调器
// 左栏：角色切换 + 立绘 + HP/MP + 属性 + 装备槽（EquipmentSlotView）
// 中栏：上半物品来源区（详情/商店/战利品 ShopGridView） + 下半网格背包（GridInventoryView）
// 右栏：队伍成员列表（可收起）
//
// 拖拽全部由 DragController 调度 — 本类只负责构建 UI 布局和数据绑定。
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.View.UI.Inventory;

namespace BladeHex.View.UI.Overworld;

[GlobalClass]
public partial class PartyPanel : PanelContainer
{
    [Signal] public delegate void PanelClosedEventHandler();
    // ========================================
    // 主题
    // ========================================
    private static readonly Color BgPrimary = new(0.06f, 0.06f, 0.08f, 0.95f);
    private static readonly Color BgSecondary = new(0.10f, 0.10f, 0.12f, 0.85f);
    private static readonly Color BgGrid = new(0.04f, 0.04f, 0.05f, 0.95f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);
    private static readonly Color TextPositive = new(0.3f, 0.85f, 0.3f);
    private static readonly Color TextNegative = new(0.9f, 0.3f, 0.25f);

    // ========================================
    // 数据
    // ========================================
    private PartyRoster? _roster;
    private PartyInventory? _inventory;
    private GridInventory? _gridInventory;
    private int _currentIndex = 0;

    // 商店/战利品模式
    private bool _isShopMode = false;
    private List<ItemData>? _shopStock;
    private EconomyManager? _shopEconomy;
    private int _shopProsperity = 50;
    private string _shopName = "";

    private UnitData? SelectedUnit => _roster != null && _currentIndex >= 0 && _currentIndex < _roster.Members.Count
        ? _roster.Members[_currentIndex] : null;

    // ========================================
    // UI 组件
    // ========================================
    private VBoxContainer _leftCol = null!;
    private VBoxContainer _centerCol = null!;
    private VBoxContainer _rightCol = null!;
    private PanelContainer _rightPanel = null!;
    private Label _nameLabel = null!;
    private Label _indexLabel = null!;
    private Button _toggleArmyBtn = null!;
    private bool _armyPanelVisible = true;

    private GridInventoryView _backpackView = null!;
    private EquipmentSlotView _equipmentView = null!;
    private ShopGridView? _shopView;
    private Label? _shopGoldLabel;

    private DragController _dragController = null!;
    private ItemPopup _itemPopup = null!;

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        _BuildLayout();
    }

    // ========================================
    // 公开 API
    // ========================================

    public void Open(PartyRoster roster, PartyInventory inventory)
    {
        _roster = roster;
        _inventory = inventory;
        _currentIndex = 0;
        _isShopMode = false;
        _shopStock = null;
        Visible = true;
        _EnsureGridInventory();
        _RefreshAll();
    }

    public void OpenShop(PartyRoster roster, PartyInventory inventory, string shopName, EconomyManager economy, List<ItemData> stock, int prosperity = 50)
    {
        _roster = roster;
        _inventory = inventory;
        _currentIndex = 0;
        _isShopMode = true;
        _shopStock = stock;
        _shopEconomy = economy;
        _shopProsperity = prosperity;
        _shopName = shopName;
        Visible = true;
        _EnsureGridInventory();
        _RefreshAll();
    }

    public void OpenLoot(PartyRoster roster, PartyInventory inventory, List<ItemData> loot, int goldGranted = 0, int xpGranted = 0)
    {
        _roster = roster;
        _inventory = inventory;
        _currentIndex = 0;
        _isShopMode = true;
        _shopStock = loot;
        _shopEconomy = null; // 战利品模式
        _shopProsperity = 50;
        _shopName = $"战利品 — 金币+{goldGranted} 经验+{xpGranted}";
        Visible = true;
        _EnsureGridInventory();
        _RefreshAll();
    }

    public void OpenTab(string tabName, UnitData? unitData = null)
    {
        Visible = true;
        _RefreshAll();
    }

    public void RefreshUi()
    {
        if (!IsInsideTree() || !Visible) return;
        _RefreshAll();
    }

    // ========================================
    // UI 构建
    // ========================================

    private void _BuildLayout()
    {
        // 面板尺寸
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AnchorLeft = 0.12f; AnchorRight = 0.88f;
        AnchorTop = 0.08f; AnchorBottom = 0.92f;
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

        // 顶栏
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        mainVbox.AddChild(header);

        var title = _MakeLabel("部队管理", 20, TextAccent);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        _toggleArmyBtn = new Button { Text = "军队 ▶", CustomMinimumSize = new Vector2(80, 32) };
        _toggleArmyBtn.AddThemeFontSizeOverride("font_size", 12);
        _toggleArmyBtn.Pressed += _ToggleArmyPanel;
        header.AddChild(_toggleArmyBtn);

        var closeBtn = new Button { Text = "✕", CustomMinimumSize = new Vector2(32, 32) };
        closeBtn.Pressed += () =>
        {
            Visible = false;
            _isShopMode = false;
            _shopStock = null;
            _shopEconomy = null;
            EmitSignal(SignalName.PanelClosed);
        };
        header.AddChild(closeBtn);

        mainVbox.AddChild(new HSeparator());

        // 三栏
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 12);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        mainVbox.AddChild(columns);

        // 左栏
        var leftPanel = _MakeColumnPanel(260);
        columns.AddChild(leftPanel);
        var leftScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        leftScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        leftPanel.AddChild(leftScroll);
        _leftCol = new VBoxContainer();
        _leftCol.AddThemeConstantOverride("separation", 6);
        _leftCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leftScroll.AddChild(_leftCol);

        // 中栏
        var centerPanel = _MakeColumnPanel(0);
        centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        columns.AddChild(centerPanel);
        _centerCol = new VBoxContainer();
        _centerCol.AddThemeConstantOverride("separation", 6);
        _centerCol.SizeFlagsVertical = SizeFlags.ExpandFill;
        _centerCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        centerPanel.AddChild(_centerCol);

        // 右栏
        _rightPanel = _MakeColumnPanel(240);
        columns.AddChild(_rightPanel);
        var rightScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        rightScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        _rightPanel.AddChild(rightScroll);
        _rightCol = new VBoxContainer();
        _rightCol.AddThemeConstantOverride("separation", 4);
        _rightCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rightScroll.AddChild(_rightCol);

        // 拖拽控制器（持久存在，不随刷新销毁）
        _dragController = new DragController { Name = "DragController" };
        AddChild(_dragController);
        _dragController.SetGhostParent(this);

        // 物品详情弹窗
        _itemPopup = new ItemPopup { Name = "ItemPopup" };
        AddChild(_itemPopup);

        // 拖拽开始时自动隐藏详情弹窗
        _dragController.DragStarted += () => _itemPopup.Hide();
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

    // ========================================
    // 数据初始化
    // ========================================

    private void _EnsureGridInventory()
    {
        if (_gridInventory != null) return;
        _gridInventory = new GridInventory();
        if (_roster != null && _roster.Members.Count > 0)
            _gridInventory.RecalculateCapacity(_roster.Members);

        if (_inventory != null)
        {
            foreach (var slot in _inventory.Slots)
            {
                ItemData? resolved = _ResolveItemFromSlot(slot);
                if (resolved == null)
                {
                    resolved = new ItemData
                    {
                        ItemName = slot.ItemName,
                        ItemId = slot.ItemName,
                        Price = slot.Value,
                        Description = slot.Description,
                    };
                }
                ItemSizeConfig.ApplyRecommendedSize(resolved);
                _gridInventory.TryAutoPlace(resolved, slot.Quantity);
            }
        }
        _gridInventory.AutoSort();
    }

    private static ItemData? _ResolveItemFromSlot(InventorySlot slot)
    {
        var weapons = ItemDataLoader.GetWeapons();
        if (weapons.TryGetValue(slot.ItemName, out var w)) return w;
        var armors = ItemDataLoader.GetArmors();
        if (armors.TryGetValue(slot.ItemName, out var a)) return a;
        var consumables = ItemDataLoader.GetConsumables();
        if (consumables.TryGetValue(slot.ItemName, out var c)) return c;
        var quivers = ItemDataLoader.GetQuivers();
        if (quivers.TryGetValue(slot.ItemName, out var q)) return q;
        foreach (var kvp in weapons) if (kvp.Value.ItemName == slot.ItemName) return kvp.Value;
        foreach (var kvp in armors) if (kvp.Value.ItemName == slot.ItemName) return kvp.Value;
        foreach (var kvp in consumables) if (kvp.Value.ItemName == slot.ItemName) return kvp.Value;
        return null;
    }

    // ========================================
    // 三栏刷新
    // ========================================

    private void _RefreshAll()
    {
        // 重建拖拽容器注册
        _dragController.ClearContainers();
        _itemPopup.Hide();

        _RefreshLeft();
        _RefreshCenter();
        _RefreshRight();
    }

    // ─── 左栏：角色切换 + 立绘 + 装备 ───

    private void _RefreshLeft()
    {
        foreach (Node c in _leftCol.GetChildren()) c.QueueFree();

        // 角色切换
        var switchRow = new HBoxContainer();
        switchRow.Alignment = BoxContainer.AlignmentMode.Center;
        switchRow.AddThemeConstantOverride("separation", 8);
        _leftCol.AddChild(switchRow);

        var prevBtn = new Button { Text = "◀", CustomMinimumSize = new Vector2(36, 36) };
        prevBtn.Pressed += () => _SwitchChar(-1);
        switchRow.AddChild(prevBtn);

        int total = _roster?.Members.Count ?? 0;
        _nameLabel = _MakeLabel(SelectedUnit?.UnitName ?? "无角色", 16, TextAccent);
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        switchRow.AddChild(_nameLabel);

        var nextBtn = new Button { Text = "▶", CustomMinimumSize = new Vector2(36, 36) };
        nextBtn.Pressed += () => _SwitchChar(1);
        switchRow.AddChild(nextBtn);

        _indexLabel = _MakeLabel($"({_currentIndex + 1}/{total})", 11, TextMuted);
        _indexLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _leftCol.AddChild(_indexLabel);

        var u = SelectedUnit;
        if (u == null) return;

        // 立绘
        var avatar = new BladeHex.View.Unit.CharacterAvatarControl
        {
            Mode = BladeHex.View.Unit.CharacterAvatarControl.DisplayMode.Full,
            CustomMinimumSize = new Vector2(180, 220),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        _leftCol.AddChild(avatar);
        avatar.SetUnit(u);

        // HP/MP
        int hp = PartyRoster.GetCurrentHp(u);
        int maxHp = u.BaseMaxHp + (int)Math.Floor(Math.Sqrt(u.Con / 4.0)) * u.Level;
        _leftCol.AddChild(_MakeBar("生命", hp, maxHp, TextPositive));
        int maxMana = Math.Max(1, u.Intel / 2 + u.Level);
        _leftCol.AddChild(_MakeBar("法力", u.CurrentMana, maxMana, new Color(0.3f, 0.5f, 1.0f)));

        // 等级单独一行
        var lvLine = _MakeLabel($"等级 {u.Level}", 12, TextAccent);
        lvLine.HorizontalAlignment = HorizontalAlignment.Center;
        _leftCol.AddChild(lvLine);

        // 六维（Grid 排列保证对齐）
        var statBlock = new GridContainer { Columns = 3 };
        statBlock.AddThemeConstantOverride("h_separation", 10);
        statBlock.AddThemeConstantOverride("v_separation", 2);
        _leftCol.AddChild(statBlock);
        _AddInlineStat(statBlock, "力量", u.Str);
        _AddInlineStat(statBlock, "敏捷", u.Dex);
        _AddInlineStat(statBlock, "体质", u.Con);
        _AddInlineStat(statBlock, "智力", u.Intel);
        _AddInlineStat(statBlock, "感知", u.Wis);
        _AddInlineStat(statBlock, "魅力", u.Cha);

        _leftCol.AddChild(new HSeparator());

        // 装备槽
        _equipmentView = new EquipmentSlotView { Name = "EquipmentSlotView" };
        _leftCol.AddChild(_equipmentView);
        _equipmentView.Initialize(u, _dragController, _itemPopup);

        _leftCol.AddChild(new HSeparator());

        // 战斗属性
        _BuildStatGrid(u);
    }

    private void _BuildStatGrid(UnitData u)
    {
        // 战斗属性面板：2列卡片式布局
        _leftCol.AddChild(_MakeLabel("战斗属性", 11, TextAccent));

        var statGrid = new GridContainer { Columns = 2 };
        statGrid.AddThemeConstantOverride("h_separation", 8);
        statGrid.AddThemeConstantOverride("v_separation", 4);
        statGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _leftCol.AddChild(statGrid);

        int dexMod = (int)Math.Floor(Math.Sqrt(u.Dex / 2.0));
        if (u.Armor != null && u.Armor.MaxDexBonus < 99)
            dexMod = Math.Min(dexMod, u.Armor.MaxDexBonus);
        int drTotal = u.Armor?.DrThreshold ?? 0;
        int ac = 10 + dexMod + (int)Math.Floor(Math.Sqrt(drTotal));
        _AddStatCard(statGrid, "闪避", ac.ToString());

        int conMod = (int)Math.Floor(Math.Sqrt(u.Con / 2.0));
        int apPenalty = u.Armor?.ApPenalty ?? 0;
        int maxAp = 12 + (int)Math.Floor(Math.Sqrt(u.Dex / 2.0)) + conMod / 2 - apPenalty;
        _AddStatCard(statGrid, "行动力", $"{maxAp}");

        int dmgMin = 1, dmgMax = 3;
        if (u.PrimaryMainHand != null)
        {
            dmgMin = u.PrimaryMainHand.DamageDiceCount;
            dmgMax = u.PrimaryMainHand.DamageDiceCount * u.PrimaryMainHand.DamageDiceSides;
        }
        _AddStatCard(statGrid, "伤害", $"{dmgMin}-{dmgMax}");

        int wisCritTier = (int)Math.Floor(Math.Sqrt(Math.Max(0, u.Wis - 14) / 4.0));
        int critPct = 5 + wisCritTier * 5;
        _AddStatCard(statGrid, "暴击", $"{critPct}%");
    }

    /// <summary>添加战斗属性卡片（带边框背景，更易读）</summary>
    private static void _AddStatCard(GridContainer grid, string name, string value)
    {
        var card = new PanelContainer();
        var s = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.10f, 0.7f) };
        s.SetCornerRadiusAll(3);
        s.SetContentMarginAll(5);
        card.AddThemeStyleboxOverride("panel", s);
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(card);

        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 6);
        card.AddChild(hb);

        var n = new Label { Text = name };
        n.AddThemeFontSizeOverride("font_size", 11);
        n.AddThemeColorOverride("font_color", TextMuted);
        hb.AddChild(n);

        var v = new Label { Text = value };
        v.AddThemeFontSizeOverride("font_size", 13);
        v.AddThemeColorOverride("font_color", TextPrimary);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        v.HorizontalAlignment = HorizontalAlignment.Right;
        hb.AddChild(v);
    }

    private void _SwitchChar(int dir)
    {
        if (_roster == null || _roster.Members.Count == 0) return;
        _currentIndex = (_currentIndex + dir + _roster.Members.Count) % _roster.Members.Count;
        _RefreshAll();
    }

    // ─── 中栏：商店/详情区 + 背包 ───

    private void _RefreshCenter()
    {
        foreach (Node c in _centerCol.GetChildren()) c.QueueFree();
        _shopView = null;
        _shopGoldLabel = null;

        // 上半：商店或详情提示
        if (_isShopMode && _shopStock != null)
            _BuildShopArea();
        else
            _BuildDetailHint();

        _centerCol.AddChild(new HSeparator());

        // 下半：网格背包
        var invHeader = new HBoxContainer();
        invHeader.AddThemeConstantOverride("separation", 6);
        _centerCol.AddChild(invHeader);
        invHeader.AddChild(_MakeLabel("背包", 13, TextAccent));

        var capLbl = _MakeLabel("", 10, TextSecondary);
        capLbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        invHeader.AddChild(capLbl);

        var sortBtn = new Button { Text = "整理", CustomMinimumSize = new Vector2(48, 22) };
        sortBtn.AddThemeFontSizeOverride("font_size", 11);
        sortBtn.Pressed += () => { _gridInventory?.AutoSort(); _backpackView?.Refresh(); };
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

        var gridScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        gridScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        gridOuter.AddChild(gridScroll);

        _backpackView = new GridInventoryView { Name = "BackpackView" };
        gridScroll.AddChild(_backpackView);
        if (_gridInventory != null)
        {
            _backpackView.Initialize(_gridInventory, _dragController, _itemPopup);
            capLbl.Text = $"{_gridInventory.UsedCells}/{_gridInventory.TotalCells}";
        }
    }

    private void _BuildDetailHint()
    {
        var hint = new PanelContainer();
        var s = new StyleBoxFlat { BgColor = new Color(0.07f, 0.07f, 0.09f, 0.6f) };
        s.SetCornerRadiusAll(4);
        s.SetContentMarginAll(8);
        hint.AddThemeStyleboxOverride("panel", s);
        hint.CustomMinimumSize = new Vector2(0, 80);
        _centerCol.AddChild(hint);

        var lbl = _MakeLabel("右键点击物品查看详情", 12, TextMuted);
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AddChild(lbl);
    }

    private void _BuildShopArea()
    {
        var shopPanel = new PanelContainer();
        var s = new StyleBoxFlat { BgColor = new Color(0.08f, 0.06f, 0.04f, 0.9f) };
        s.SetBorderWidthAll(1);
        s.BorderColor = new Color(0.6f, 0.5f, 0.25f, 0.7f);
        s.SetCornerRadiusAll(4);
        s.SetContentMarginAll(8);
        shopPanel.AddThemeStyleboxOverride("panel", s);
        shopPanel.CustomMinimumSize = new Vector2(0, 140);
        shopPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        _centerCol.AddChild(shopPanel);

        var shopVbox = new VBoxContainer();
        shopVbox.AddThemeConstantOverride("separation", 6);
        shopPanel.AddChild(shopVbox);

        // 标题
        var shopHeader = new HBoxContainer();
        shopHeader.AddThemeConstantOverride("separation", 8);
        shopVbox.AddChild(shopHeader);

        shopHeader.AddChild(_MakeLabel(_shopName, 13, TextAccent));
        shopHeader.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        if (_shopEconomy != null)
        {
            _shopGoldLabel = _MakeLabel($"金币: {_shopEconomy.Gold}", 12, TextPositive);
            shopHeader.AddChild(_shopGoldLabel);
        }

        // 滚动 + 网格视图
        var gridOuter = new PanelContainer();
        var outerStyle = new StyleBoxFlat { BgColor = BgGrid };
        outerStyle.SetBorderWidthAll(1);
        outerStyle.BorderColor = new Color(0.4f, 0.35f, 0.2f, 0.6f);
        outerStyle.SetCornerRadiusAll(3);
        outerStyle.SetContentMarginAll(4);
        gridOuter.AddThemeStyleboxOverride("panel", outerStyle);
        gridOuter.SizeFlagsVertical = SizeFlags.ExpandFill;
        shopVbox.AddChild(gridOuter);

        var gridScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        gridScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        gridOuter.AddChild(gridScroll);

        _shopView = new ShopGridView
        {
            Name = "ShopGridView",
            Economy = _shopEconomy,
            Prosperity = _shopProsperity,
        };
        _shopView.OnGoldChanged = (gold) =>
        {
            if (_shopGoldLabel != null) _shopGoldLabel.Text = $"金币: {gold}";
        };
        gridScroll.AddChild(_shopView);
        if (_shopStock != null)
            _shopView.Initialize(_shopStock, _dragController, _itemPopup);

        // 提示
        string hint = _shopEconomy != null ? "拖入背包购买 · 拖出商店卖出" : "拖入背包拾取";
        shopVbox.AddChild(_MakeLabel(hint, 10, TextMuted));
    }

    // ─── 右栏：队伍成员 ───

    private void _RefreshRight()
    {
        foreach (Node c in _rightCol.GetChildren()) c.QueueFree();

        _rightCol.AddChild(_MakeLabel("队伍成员", 14, TextAccent));
        _rightCol.AddChild(new HSeparator());

        if (_roster == null || _roster.Members.Count == 0)
        {
            _rightCol.AddChild(_MakeLabel("暂无队伍成员", 12, TextMuted));
            return;
        }

        int totalHp = 0, totalMaxHp = 0;
        foreach (var member in _roster.Members)
        {
            int hp = PartyRoster.GetCurrentHp(member);
            int maxHp = member.BaseMaxHp;
            totalHp += hp; totalMaxHp += maxHp;

            bool isLeader = _roster.IsLeader(member);
            string prefix = isLeader ? "★ " : "";
            _AddMemberEntry($"{prefix}{member.UnitName}", member.Level, hp, maxHp, member.Morale);
        }

        _rightCol.AddChild(new HSeparator());
        _rightCol.AddChild(_MakeLabel($"队伍人数: {_roster.Count}/{_roster.Capacity}", 12, TextAccent));
        float hpPct = totalMaxHp > 0 ? (float)totalHp / totalMaxHp * 100f : 0;
        _rightCol.AddChild(_MakeLabel($"整体状态: {hpPct:F0}%", 11,
            hpPct > 60 ? TextPositive : hpPct > 30 ? TextAccent : TextNegative));
    }

    private void _AddMemberEntry(string name, int level, int hp, int maxHp, int morale)
    {
        var entry = new PanelContainer();
        var entryStyle = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.10f, 0.5f) };
        entryStyle.SetCornerRadiusAll(3);
        entryStyle.SetContentMarginAll(6);
        entry.AddThemeStyleboxOverride("panel", entryStyle);
        _rightCol.AddChild(entry);

        var inner = new VBoxContainer();
        inner.AddThemeConstantOverride("separation", 3);
        entry.AddChild(inner);

        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 4);
        inner.AddChild(topRow);

        topRow.AddChild(_MakeLabel($"{name} Lv.{level}", 12, TextPrimary));
        var hpStr = _MakeLabel($"{hp}/{maxHp}", 10, hp > maxHp / 2 ? TextPositive : TextNegative);
        hpStr.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hpStr.HorizontalAlignment = HorizontalAlignment.Right;
        topRow.AddChild(hpStr);

        var hpRow = new HBoxContainer();
        hpRow.AddThemeConstantOverride("separation", 4);
        inner.AddChild(hpRow);
        hpRow.AddChild(_MakeLabel("HP", 9, TextMuted));

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = Math.Max(maxHp, 1),
            Value = hp,
            ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(80, 8),
        };
        var hpColor = hp > maxHp * 0.6f ? TextPositive : hp > maxHp * 0.3f ? TextAccent : TextNegative;
        var hpBg = new StyleBoxFlat { BgColor = new Color(0.04f, 0.04f, 0.05f, 0.9f) };
        hpBg.SetCornerRadiusAll(2);
        bar.AddThemeStyleboxOverride("background", hpBg);
        var fill = new StyleBoxFlat { BgColor = hpColor };
        fill.SetCornerRadiusAll(2);
        bar.AddThemeStyleboxOverride("fill", fill);
        hpRow.AddChild(bar);

        if (morale > 0)
        {
            var mc = morale >= 70 ? TextPositive : morale >= 40 ? TextAccent : TextNegative;
            hpRow.AddChild(_MakeLabel($"♥{morale}", 9, mc));
        }
    }

    // ========================================
    // UI 工具
    // ========================================

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

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = Math.Max(max, 1),
            Value = current,
            ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(120, 16),
        };
        // 背景槽
        var bg = new StyleBoxFlat { BgColor = new Color(0.04f, 0.04f, 0.05f, 0.9f) };
        bg.SetCornerRadiusAll(3);
        bg.SetBorderWidthAll(1);
        bg.BorderColor = new Color(0.2f, 0.18f, 0.16f, 0.7f);
        bar.AddThemeStyleboxOverride("background", bg);
        // 填充
        var fill = new StyleBoxFlat { BgColor = barColor };
        fill.SetCornerRadiusAll(3);
        bar.AddThemeStyleboxOverride("fill", fill);
        hbox.AddChild(bar);

        var val = new Label { Text = $"{current}/{max}" };
        val.AddThemeFontSizeOverride("font_size", 11);
        val.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
        hbox.AddChild(val);

        return hbox;
    }

    private static void _AddStatPair(GridContainer grid, string name, string value)
    {
        var n = new Label { Text = name };
        n.AddThemeFontSizeOverride("font_size", 13);
        n.AddThemeColorOverride("font_color", TextSecondary);
        grid.AddChild(n);

        var v = new Label { Text = value };
        v.AddThemeFontSizeOverride("font_size", 13);
        grid.AddChild(v);
    }

    /// <summary>添加六维属性单元（"力量 12" 形式，左标签右数字）</summary>
    private static void _AddInlineStat(GridContainer grid, string name, int value)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);

        var n = new Label { Text = name };
        n.AddThemeFontSizeOverride("font_size", 11);
        n.AddThemeColorOverride("font_color", TextMuted);
        hbox.AddChild(n);

        var v = new Label { Text = value.ToString() };
        v.AddThemeFontSizeOverride("font_size", 11);
        v.AddThemeColorOverride("font_color", TextPrimary);
        hbox.AddChild(v);

        grid.AddChild(hbox);
    }

    /// <summary>关闭商店模式（外部调用）</summary>
    public void CloseShop()
    {
        _isShopMode = false;
        _shopStock = null;
        _shopEconomy = null;
    }
}
