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
    private VBoxContainer? _attributesBox;

    private DragController _dragController = null!;
    private ItemPopup _itemPopup = null!;

    // ========================================
    // 生命周期
    // ========================================

    public override void _Ready()
    {
        _BuildLayout();

        // 初次进入和窗口尺寸变化时都要对齐 HUD
        GetTree().Root.SizeChanged += _AlignToHud;
        VisibilityChanged += () => { if (Visible) _AlignToHud(); };
    }

    /// <summary>
    /// 把面板上下边界对齐到顶部状态栏底边和底部功能栏顶边（紧贴 HUD 不留缝隙、不遮挡）。
    /// </summary>
    private void _AlignToHud()
    {
        if (!IsInsideTree()) return;

        Control? topBar = null;
        Control? botBar = null;

        // 沿父链找 OverworldUI（提供 TopPanel / BottomPanel）
        Node? n = GetParent();
        while (n != null)
        {
            if (n is OverworldUI hud)
            {
                topBar = hud.TopPanel;
                botBar = hud.BottomPanel;
                break;
            }
            n = n.GetParent();
        }

        // 没找到 HUD：用一个保守默认值
        float topH = topBar != null ? topBar.Size.Y : 48f;
        float botH = botBar != null ? botBar.Size.Y : 96f;

        OffsetTop = topH + 8f;
        OffsetBottom = -(botH + 8f);

        // HUD 控件可能在打开瞬间尺寸还未结算，等下一帧再次对齐一次
        CallDeferred(nameof(_AlignToHudDeferred));
    }

    private void _AlignToHudDeferred()
    {
        if (!IsInsideTree() || !Visible) return;

        Control? topBar = null;
        Control? botBar = null;
        Node? n = GetParent();
        while (n != null)
        {
            if (n is OverworldUI hud)
            {
                topBar = hud.TopPanel;
                botBar = hud.BottomPanel;
                break;
            }
            n = n.GetParent();
        }

        if (topBar == null && botBar == null) return;
        float topH = topBar != null ? topBar.Size.Y : 48f;
        float botH = botBar != null ? botBar.Size.Y : 96f;
        OffsetTop = topH + 8f;
        OffsetBottom = -(botH + 8f);
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
        _AlignToHud();
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
        _AlignToHud();
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
        _AlignToHud();
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
        // 面板尺寸：紧贴顶部状态栏底边到底部功能栏顶边，水平稍内缩
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SizeFlagsHorizontal = SizeFlags.Fill;
        SizeFlagsVertical = SizeFlags.Fill;
        AnchorLeft = 0; AnchorRight = 1;
        AnchorTop = 0; AnchorBottom = 1;
        // OffsetTop/Bottom 在 _AlignToHud() 中按运行时 HUD 实际高度设置
        OffsetLeft = 32; OffsetRight = -32;
        OffsetTop = 0; OffsetBottom = 0;

        var style = new StyleBoxFlat { BgColor = BgPrimary };
        style.SetBorderWidthAll(2);
        style.BorderColor = BorderHighlight;
        style.SetCornerRadiusAll(6);
        AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", 4);
        mainVbox.SizeFlagsVertical = SizeFlags.ExpandFill;
        margin.AddChild(mainVbox);

        // 顶栏（已移除"部队管理"标题；军队按钮移到背包头部；这里只放关闭按钮，浮在右上）
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        mainVbox.AddChild(header);

        // 占位 — 让关闭按钮靠右
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var closeBtn = new Button { Text = "✕", CustomMinimumSize = new Vector2(48, 48) };
        closeBtn.AddThemeFontSizeOverride("font_size", 22);
        closeBtn.Pressed += () =>
        {
            Visible = false;
            _isShopMode = false;
            _shopStock = null;
            _shopEconomy = null;
            EmitSignal(SignalName.PanelClosed);
        };
        header.AddChild(closeBtn);

        // 三栏
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 12);
        columns.SizeFlagsVertical = SizeFlags.ExpandFill;
        mainVbox.AddChild(columns);

        // 左栏
        var leftPanel = _MakeColumnPanel(380);
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

        // 中栏 + 右侧"军队"竖按钮（取代旧顶栏的横向按钮）
        var centerWrap = new HBoxContainer();
        centerWrap.AddThemeConstantOverride("separation", 4);
        centerWrap.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        centerWrap.SizeFlagsVertical = SizeFlags.ExpandFill;
        columns.AddChild(centerWrap);

        var centerPanel = _MakeColumnPanel(0);
        centerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        centerWrap.AddChild(centerPanel);
        _centerCol = new VBoxContainer();
        _centerCol.AddThemeConstantOverride("separation", 6);
        _centerCol.SizeFlagsVertical = SizeFlags.ExpandFill;
        _centerCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        centerPanel.AddChild(_centerCol);

        // 军队竖按钮（每个汉字一行 → 自动竖排）
        _toggleArmyBtn = new Button
        {
            Text = "军\n队\n▶",
            CustomMinimumSize = new Vector2(48, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _toggleArmyBtn.AddThemeFontSizeOverride("font_size", 22);
        _toggleArmyBtn.Pressed += _ToggleArmyPanel;
        centerWrap.AddChild(_toggleArmyBtn);

        // 右栏
        _rightPanel = _MakeColumnPanel(420);
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
        _toggleArmyBtn.Text = _armyPanelVisible ? "军\n队\n▶" : "◀\n军\n队";
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

    // ─── 左栏：角色切换 + HP/MP + 属性 + 立绘叠装备 ───

    private void _RefreshLeft()
    {
        foreach (Node c in _leftCol.GetChildren()) c.QueueFree();

        // 角色切换
        var switchRow = new HBoxContainer();
        switchRow.Alignment = BoxContainer.AlignmentMode.Center;
        switchRow.AddThemeConstantOverride("separation", 8);
        _leftCol.AddChild(switchRow);

        var prevBtn = new Button { Text = "◀", CustomMinimumSize = new Vector2(56, 56) };
        prevBtn.AddThemeFontSizeOverride("font_size", 22);
        prevBtn.Pressed += () => _SwitchChar(-1);
        switchRow.AddChild(prevBtn);

        int total = _roster?.Members.Count ?? 0;
        _nameLabel = _MakeLabel(SelectedUnit?.UnitName ?? "无角色", 32, TextAccent);
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        switchRow.AddChild(_nameLabel);

        var nextBtn = new Button { Text = "▶", CustomMinimumSize = new Vector2(56, 56) };
        nextBtn.AddThemeFontSizeOverride("font_size", 22);
        nextBtn.Pressed += () => _SwitchChar(1);
        switchRow.AddChild(nextBtn);

        var u = SelectedUnit;
        if (u == null) return;

        // 等级 + 职业称号 + 序号同行（紧凑）
        var lvRow = new HBoxContainer();
        lvRow.Alignment = BoxContainer.AlignmentMode.Center;
        lvRow.AddThemeConstantOverride("separation", 12);
        _leftCol.AddChild(lvRow);
        var lvLine = _MakeLabel($"等级 {u.Level}", 22, TextAccent);
        lvRow.AddChild(lvLine);

        // 职业称号（从技能盘推导）+ 图标
        string classTitle = _GetClassTitle(u);
        if (!string.IsNullOrEmpty(classTitle))
        {
            var titleLine = _MakeLabel(classTitle, 20, new Color(0.8f, 0.7f, 1.0f));
            lvRow.AddChild(titleLine);

            // 职业图标（64x64 缩放到行高）
            string? iconPath = ClassTitleResolver.GetIconPath(classTitle);
            if (iconPath != null)
            {
                var iconTex = GD.Load<Texture2D>(iconPath);
                if (iconTex != null)
                {
                    var icon = new TextureRect();
                    icon.Texture = iconTex;
                    icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                    icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                    icon.CustomMinimumSize = new Vector2(32, 32);
                    lvRow.AddChild(icon);
                }
            }
        }

        var idxLine = _MakeLabel($"({_currentIndex + 1}/{total})", 18, TextMuted);
        lvRow.AddChild(idxLine);
        _indexLabel = idxLine;

        // HP/MP
        int hp = PartyRoster.GetCurrentHp(u);
        int maxHp = u.BaseMaxHp + (int)Math.Floor(Math.Sqrt(u.Con / 4.0)) * u.Level;
        _leftCol.AddChild(_MakeBar("生命", hp, maxHp, TextPositive));
        int maxMana = Math.Max(1, u.Intel / 2 + u.Level);
        _leftCol.AddChild(_MakeBar("法力", u.CurrentMana, maxMana, new Color(0.3f, 0.5f, 1.0f)));

        // 立绘 + 装备槽（叠层：立绘在底，装备槽在上；鼠标悬停装备区时整体透明，露出立绘）
        _BuildAvatarEquipStack(u);

        _leftCol.AddChild(new HSeparator());

        // 属性（六维 + 战斗属性合并紧凑显示）
        _BuildCompactAttributes(u);
    }

    /// <summary>
    /// 构建立绘+装备叠层容器：立绘作为背景，装备槽悬浮在上方。
    /// 鼠标悬停在装备区域时，装备整体降透明度，方便观察立绘。
    /// </summary>
    private void _BuildAvatarEquipStack(UnitData u)
    {
        // 用尺寸固定的容器承载立绘和装备槽
        var stack = new Control
        {
            CustomMinimumSize = new Vector2(0, 320),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _leftCol.AddChild(stack);

        // 立绘（底层，铺满容器）
        var avatar = new BladeHex.View.Unit.CharacterAvatarControl
        {
            Mode = BladeHex.View.Unit.CharacterAvatarControl.DisplayMode.Full,
            CustomMinimumSize = new Vector2(280, 280),
        };
        avatar.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        avatar.MouseFilter = Control.MouseFilterEnum.Ignore;
        stack.AddChild(avatar);
        avatar.SetUnit(u);

        // 装备槽（顶层，水平居中；包一层 CenterContainer 让 VBox 按内容尺寸居中）
        var equipCenter = new CenterContainer();
        equipCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        equipCenter.MouseFilter = Control.MouseFilterEnum.Pass;
        stack.AddChild(equipCenter);

        _equipmentView = new EquipmentSlotView { Name = "EquipmentSlotView" };
        equipCenter.AddChild(_equipmentView);
        _equipmentView.Initialize(u, _dragController, _itemPopup);

        // 装备变化时实时刷新属性面板（不重建装备视图本身）
        _equipmentView.EquipmentChanged += () => _RefreshAttributesOnly();

        // 鼠标悬停整体淡化，露出立绘 — 用每帧检测，避免子槽位 Stop 过滤导致的 Enter/Exit 抖动
        _equipmentView.HoverFadeEnabled = true;
    }

    /// <summary>属性面板：六维 + 战斗属性合并成一个紧凑卡片网格</summary>
    private void _BuildCompactAttributes(UnitData u)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _leftCol.AddChild(box);
        _attributesBox = box;
        _PopulateAttributes(box, u);
    }

    /// <summary>装备变化时只重建属性区域，避免拖拽视图被销毁</summary>
    private void _RefreshAttributesOnly()
    {
        var u = SelectedUnit;
        if (u == null || _attributesBox == null) return;
        foreach (Node c in _attributesBox.GetChildren()) c.QueueFree();
        _PopulateAttributes(_attributesBox, u);
    }

    private void _PopulateAttributes(VBoxContainer box, UnitData u)
    {
        box.AddChild(_MakeLabel("属性", 18, TextAccent));

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 2);
        grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.AddChild(grid);

        // 六维
        _AddStatCard(grid, "力", u.Str.ToString());
        _AddStatCard(grid, "敏", u.Dex.ToString());
        _AddStatCard(grid, "体", u.Con.ToString());
        _AddStatCard(grid, "智", u.Intel.ToString());
        _AddStatCard(grid, "感", u.Wis.ToString());
        _AddStatCard(grid, "魅", u.Cha.ToString());

        // 战斗属性（实时计算）
        int dexMod = (int)Math.Floor(Math.Sqrt(u.Dex / 2.0));
        if (u.Armor != null && u.Armor.MaxDexBonus < 99)
            dexMod = Math.Min(dexMod, u.Armor.MaxDexBonus);
        int drTotal = u.Armor?.DrThreshold ?? 0;
        int ac = 10 + dexMod + (int)Math.Floor(Math.Sqrt(drTotal));
        _AddStatCard(grid, "闪避", ac.ToString());

        int conMod = (int)Math.Floor(Math.Sqrt(u.Con / 2.0));
        int apPenalty = u.Armor?.ApPenalty ?? 0;
        int maxAp = 12 + (int)Math.Floor(Math.Sqrt(u.Dex / 2.0)) + conMod / 2 - apPenalty;
        _AddStatCard(grid, "AP", $"{maxAp}");

        int dmgMin = 1, dmgMax = 3;
        if (u.PrimaryMainHand != null)
        {
            dmgMin = u.PrimaryMainHand.DamageDiceCount;
            dmgMax = u.PrimaryMainHand.DamageDiceCount * u.PrimaryMainHand.DamageDiceSides;
        }
        _AddStatCard(grid, "伤害", $"{dmgMin}-{dmgMax}");

        int wisCritTier = (int)Math.Floor(Math.Sqrt(Math.Max(0, u.Wis - 14) / 4.0));
        int critPct = 5 + wisCritTier * 5;
        _AddStatCard(grid, "暴击", $"{critPct}%");

        // 先攻修正
        int initMod = BladeHex.Combat.CombatStats.GetInitiativeModifier(u);
        _AddStatCard(grid, "先攻", initMod >= 0 ? $"+{initMod}" : $"{initMod}");

        // 特质区域
        _PopulateTraits(box, u);
    }

    /// <summary>特质展示区域：显示角色特质列表及其实时效果</summary>
    private void _PopulateTraits(VBoxContainer box, UnitData u)
    {
        if (u.CharacterTraits == null || u.CharacterTraits.Count == 0) return;

        box.AddChild(new HSeparator());
        box.AddChild(_MakeLabel("特质", 16, TextAccent));

        var traitsVbox = new VBoxContainer();
        traitsVbox.AddThemeConstantOverride("separation", 2);
        traitsVbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.AddChild(traitsVbox);

        foreach (var trait in u.CharacterTraits)
        {
            if (trait == null) continue;
            var traitRow = _BuildTraitEntry(trait);
            traitsVbox.AddChild(traitRow);
        }
    }

    /// <summary>构建单个特质条目：图标(预留) + 名称 + 效果</summary>
    private Control _BuildTraitEntry(TraitData trait)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 6);
        hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // 图标占位（16x16 紧凑）
        if (!string.IsNullOrEmpty(trait.IconId))
        {
            var iconTex = GD.Load<Texture2D>($"res://assets/generated_ui_icons/{trait.IconId}.png");
            if (iconTex != null)
            {
                var icon = new TextureRect();
                icon.Texture = iconTex;
                icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                icon.CustomMinimumSize = new Vector2(16, 16);
                hbox.AddChild(icon);
            }
        }
        else
        {
            var dot = new ColorRect();
            dot.CustomMinimumSize = new Vector2(8, 8);
            dot.Color = _GetTraitColor(trait);
            var dotCenter = new CenterContainer();
            dotCenter.CustomMinimumSize = new Vector2(16, 16);
            dotCenter.AddChild(dot);
            hbox.AddChild(dotCenter);
        }

        // 名称（带颜色）
        var nameLabel = new Label { Text = trait.TraitName };
        nameLabel.AddThemeFontSizeOverride("font_size", 14);
        nameLabel.AddThemeColorOverride("font_color", _GetTraitColor(trait));
        hbox.AddChild(nameLabel);

        // 效果简述（灰色，右对齐）
        string effectText = _GetTraitEffectText(trait);
        if (!string.IsNullOrEmpty(effectText))
        {
            var effectLabel = new Label { Text = effectText };
            effectLabel.AddThemeFontSizeOverride("font_size", 12);
            effectLabel.AddThemeColorOverride("font_color", TextMuted);
            effectLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            effectLabel.HorizontalAlignment = HorizontalAlignment.Right;
            effectLabel.ClipText = true;
            hbox.AddChild(effectLabel);
        }

        // Tooltip
        hbox.MouseFilter = Control.MouseFilterEnum.Stop;
        hbox.TooltipText = $"{trait.TraitName}: {trait.Description}";

        return hbox;
    }

    /// <summary>获取特质颜色（正面=绿，负面=红，中性=灰）</summary>
    private static Color _GetTraitColor(TraitData trait)
    {
        // 判断是正面还是负面特质
        int totalMod = trait.StrMod + trait.DexMod + trait.ConMod + trait.IntMod + trait.WisMod + trait.ChaMod;
        if (trait.traitType == TraitData.TraitType.Functional)
        {
            // 功能性特质：根据效果判断正负
            bool isNegative = trait.FunctionalEffect is "old_wound" or "gluttony" or "timid" or "xenophobia";
            return isNegative ? new Color(0.9f, 0.4f, 0.3f) : new Color(0.4f, 0.85f, 0.5f);
        }
        if (totalMod > 0) return new Color(0.4f, 0.85f, 0.5f);
        if (totalMod < 0) return new Color(0.9f, 0.4f, 0.3f);
        return new Color(0.7f, 0.68f, 0.63f);
    }

    /// <summary>获取特质实时效果文本</summary>
    private static string _GetTraitEffectText(TraitData trait)
    {
        var parts = new System.Collections.Generic.List<string>();

        // 属性修正
        if (trait.StrMod != 0) parts.Add($"力量{trait.StrMod:+#;-#}");
        if (trait.DexMod != 0) parts.Add($"敏捷{trait.DexMod:+#;-#}");
        if (trait.ConMod != 0) parts.Add($"体质{trait.ConMod:+#;-#}");
        if (trait.IntMod != 0) parts.Add($"智力{trait.IntMod:+#;-#}");
        if (trait.WisMod != 0) parts.Add($"感知{trait.WisMod:+#;-#}");
        if (trait.ChaMod != 0) parts.Add($"魅力{trait.ChaMod:+#;-#}");

        // 功能性效果
        if (trait.traitType == TraitData.TraitType.Functional && !string.IsNullOrEmpty(trait.FunctionalEffect))
        {
            string funcDesc = trait.FunctionalEffect switch
            {
                "dark_vision" => "黑暗视觉",
                "iron_stomach" => "免疫食物中毒",
                "adaptability" => "疲劳惩罚减半",
                "thick_skin" => "物理伤害-1",
                "indomitable" => "濒死时50%保持1HP",
                "ether_resonance" => "施法恢复1d4 HP",
                "premonition" => "被伏击时获得准备轮",
                "old_wound" => "战斗开始HP-10%",
                "gluttony" => "补给消耗x1.5",
                "timid" => "HP<50%时攻击-1",
                "xenophobia" => "异族队友忠诚-10",
                "long_arm" => "近战射程+1",
                "eagle_eye" => "远程命中+1",
                "speed" => "移动速度-1",
                "alertness" => "先攻+3",
                "affinity" => "商店-15%/招募-10%",
                "spell_memory" => "法术位+1",
                "sorcerer_blood" => "天生施法者",
                "ranged_hit_minus_1" => "远程命中-1",
                _ => trait.FunctionalEffect,
            };
            parts.Add(funcDesc);
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : "";
    }

    /// <summary>添加战斗属性卡片（紧凑版）</summary>
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
        hb.AddThemeConstantOverride("separation", 4);
        card.AddChild(hb);

        var n = new Label { Text = name };
        n.AddThemeFontSizeOverride("font_size", 16);
        n.AddThemeColorOverride("font_color", TextMuted);
        hb.AddChild(n);

        var v = new Label { Text = value };
        v.AddThemeFontSizeOverride("font_size", 18);
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

        // 下半：网格背包（标题+整理按钮全部左对齐，与下方网格左边缘对齐）
        var invHeader = new HBoxContainer();
        invHeader.AddThemeConstantOverride("separation", 12);
        _centerCol.AddChild(invHeader);
        invHeader.AddChild(_MakeLabel("背包", 26, TextAccent));

        var sortBtn = new Button { Text = "整理", CustomMinimumSize = new Vector2(96, 40) };
        sortBtn.AddThemeFontSizeOverride("font_size", 22);
        sortBtn.Pressed += () => { _gridInventory?.AutoSort(); _backpackView?.Refresh(); };
        invHeader.AddChild(sortBtn);

        // 占位 — 让上面两个控件保持左对齐
        invHeader.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

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
        // 左对齐：不撑满宽度
        _backpackView.SizeFlagsHorizontal = 0;
        gridScroll.AddChild(_backpackView);
        if (_gridInventory != null)
        {
            _backpackView.Initialize(_gridInventory, _dragController, _itemPopup);
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

        var lbl = _MakeLabel("右键点击物品查看详情", 24, TextMuted);
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
        shopPanel.CustomMinimumSize = new Vector2(0, 280);
        shopPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        _centerCol.AddChild(shopPanel);

        var shopVbox = new VBoxContainer();
        shopVbox.AddThemeConstantOverride("separation", 6);
        shopPanel.AddChild(shopVbox);

        // 标题
        var shopHeader = new HBoxContainer();
        shopHeader.AddThemeConstantOverride("separation", 8);
        shopVbox.AddChild(shopHeader);

        shopHeader.AddChild(_MakeLabel(_shopName, 26, TextAccent));
        shopHeader.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        if (_shopEconomy != null)
        {
            _shopGoldLabel = _MakeLabel($"金币: {_shopEconomy.Gold}", 24, TextPositive);
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
            SizeFlagsHorizontal = 0, // 左对齐，不撑满
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
        shopVbox.AddChild(_MakeLabel(hint, 20, TextMuted));
    }

    // ─── 右栏：队伍成员 ───

    private void _RefreshRight()
    {
        foreach (Node c in _rightCol.GetChildren()) c.QueueFree();

        _rightCol.AddChild(_MakeLabel("队伍成员", 28, TextAccent));
        _rightCol.AddChild(new HSeparator());

        if (_roster == null || _roster.Members.Count == 0)
        {
            _rightCol.AddChild(_MakeLabel("暂无队伍成员", 24, TextMuted));
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
            string memberClassTitle = _GetClassTitle(member);
            _AddMemberEntry($"{prefix}{member.UnitName}", member.Level, memberClassTitle, hp, maxHp, member.Morale);
        }

        _rightCol.AddChild(new HSeparator());
        _rightCol.AddChild(_MakeLabel($"队伍人数: {_roster.Count}/{_roster.Capacity}", 24, TextAccent));
        float hpPct = totalMaxHp > 0 ? (float)totalHp / totalMaxHp * 100f : 0;
        _rightCol.AddChild(_MakeLabel($"整体状态: {hpPct:F0}%", 22,
            hpPct > 60 ? TextPositive : hpPct > 30 ? TextAccent : TextNegative));
    }

    private void _AddMemberEntry(string name, int level, string classTitle, int hp, int maxHp, int morale)
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

        string levelStr = string.IsNullOrEmpty(classTitle)
            ? $"{name} Lv.{level}"
            : $"{name} Lv.{level} {classTitle}";
        topRow.AddChild(_MakeLabel(levelStr, 24, TextPrimary));
        var hpStr = _MakeLabel($"{hp}/{maxHp}", 20, hp > maxHp / 2 ? TextPositive : TextNegative);
        hpStr.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hpStr.HorizontalAlignment = HorizontalAlignment.Right;
        topRow.AddChild(hpStr);

        var hpRow = new HBoxContainer();
        hpRow.AddThemeConstantOverride("separation", 4);
        inner.AddChild(hpRow);
        hpRow.AddChild(_MakeLabel("HP", 18, TextMuted));

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = Math.Max(maxHp, 1),
            Value = hp,
            ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(160, 16),
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
            hpRow.AddChild(_MakeLabel($"♥{morale}", 18, mc));
        }
    }

    // ========================================
    // 职业称号
    // ========================================

    /// <summary>从技能盘推导角色的复合职业称号</summary>
    private static string _GetClassTitle(UnitData unit)
    {
        var stMgr = BladeHex.Data.Globals.SkillTreesOrNull;
        if (stMgr == null) return "";

        // 优先使用 CharacterId（存档恢复时设置），否则用运行时实例 ID
        long charId = unit.CharacterId >= 0 ? unit.CharacterId : (long)unit.GetInstanceId();
        var tree = stMgr.GetSkillTree(charId);
        if (tree == null) return "";
        return tree.GetClassTitleName();
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

        var lbl = new Label { Text = label, CustomMinimumSize = new Vector2(56, 0) };
        lbl.AddThemeFontSizeOverride("font_size", 24);
        hbox.AddChild(lbl);

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = Math.Max(max, 1),
            Value = current,
            ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(120, 28),
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
        val.AddThemeFontSizeOverride("font_size", 22);
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
        hbox.AddThemeConstantOverride("separation", 6);

        var n = new Label { Text = name };
        n.AddThemeFontSizeOverride("font_size", 22);
        n.AddThemeColorOverride("font_color", TextMuted);
        hbox.AddChild(n);

        var v = new Label { Text = value.ToString() };
        v.AddThemeFontSizeOverride("font_size", 22);
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
