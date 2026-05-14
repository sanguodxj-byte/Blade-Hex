// GridInventoryPanel.cs
// 暗黑破坏神2风格网格背包UI面板
// - 物品以实际尺寸显示在网格中（占多格）
// - 支持拖拽移动物品、自动整理
// - 显示容量信息（受队伍STR/CON影响）
// - 悬停显示物品详情tooltip
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Inventory;

[GlobalClass]
public partial class GridInventoryPanel : PanelContainer
{
    // ============================================================================
    // 信号
    // ============================================================================

    [Signal] public delegate void CloseRequestedEventHandler();
    [Signal] public delegate void ItemDroppedEventHandler(string instanceId, int gridX, int gridY);
    [Signal] public delegate void ItemRightClickedEventHandler(string instanceId);
    [Signal] public delegate void ItemDoubleClickedEventHandler(string instanceId);
    [Signal] public delegate void SortRequestedEventHandler();

    // ============================================================================
    // 主题常量
    // ============================================================================

    private static readonly Color BgPrimary = new(0.06f, 0.06f, 0.08f, 0.95f);
    private static readonly Color BgGrid = new(0.04f, 0.04f, 0.05f, 0.95f);
    private static readonly Color BgCell = new(0.09f, 0.09f, 0.11f, 0.8f);
    private static readonly Color BgCellHover = new(0.15f, 0.14f, 0.18f, 0.9f);
    private static readonly Color BgCellValid = new(0.1f, 0.25f, 0.1f, 0.6f);
    private static readonly Color BgCellInvalid = new(0.3f, 0.08f, 0.08f, 0.6f);
    private static readonly Color BgItemNormal = new(0.12f, 0.11f, 0.15f, 0.95f);
    private static readonly Color BorderDefault = new(0.25f, 0.25f, 0.28f, 0.5f);
    private static readonly Color BorderHighlight = new(0.5f, 0.45f, 0.3f, 0.8f);
    private static readonly Color BorderDrag = new(0.8f, 0.7f, 0.3f, 0.9f);
    private static readonly Color TextPrimary = new(0.95f, 0.93f, 0.88f);
    private static readonly Color TextSecondary = new(0.7f, 0.68f, 0.63f);
    private static readonly Color TextMuted = new(0.5f, 0.48f, 0.45f);
    private static readonly Color TextAccent = new(0.9f, 0.8f, 0.5f);

    private const int CellSize = 48;       // 每格像素大小
    private const int CellGap = 2;         // 格间距
    private const int FontLarge = 16;
    private const int FontMed = 14;
    private const int FontSmall = 12;
    private const int FontTiny = 10;
    private const int Spacing4 = 8;
    private const int Spacing6 = 12;

    // ============================================================================
    // 状态
    // ============================================================================

    private GridInventory? _inventory;
    private Control _gridContainer = null!;
    private Label _capacityLabel = null!;
    private Label _weightLabel = null!;
    private Control _tooltipPanel = null!;
    private RichTextLabel _tooltipText = null!;

    // 拖拽状态
    private GridItem? _draggedItem;
    private Control? _dragGhost;
    private Vector2 _dragOffset;
    private bool _isDragging;
    private int _hoverCellX = -1;
    private int _hoverCellY = -1;

    // 格子引用
    private readonly Dictionary<Vector2I, Panel> _cellPanels = new();
    private readonly Dictionary<string, Control> _itemControls = new();

    public override void _Ready()
    {
        Setup();
        Visible = false;
    }

    // ============================================================================
    // 公开接口
    // ============================================================================

    /// <summary>绑定背包数据并刷新显示</summary>
    public void BindInventory(GridInventory inventory)
    {
        _inventory = inventory;
        RebuildGrid();
        RefreshItems();
        UpdateCapacityDisplay();
    }

    /// <summary>刷新物品显示（数据变化后调用）</summary>
    public void RefreshItems()
    {
        ClearItemControls();
        if (_inventory == null) return;

        foreach (var gridItem in _inventory.Items)
            CreateItemControl(gridItem);

        UpdateCapacityDisplay();
    }

    /// <summary>显示面板</summary>
    public new void Show()
    {
        Visible = true;
        RefreshItems();
    }

    /// <summary>隐藏面板</summary>
    public new void Hide()
    {
        Visible = false;
        CancelDrag();
    }

    // ============================================================================
    // UI构建
    // ============================================================================

    private void Setup()
    {
        // 面板样式
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = BgPrimary;
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = BorderHighlight;
        panelStyle.SetCornerRadiusAll(4);
        panelStyle.ShadowColor = new Color(0, 0, 0, 0.6f);
        panelStyle.ShadowSize = 6;
        AddThemeStyleboxOverride("panel", panelStyle);

        var rootMargin = new MarginContainer();
        rootMargin.AddThemeConstantOverride("margin_left", Spacing6);
        rootMargin.AddThemeConstantOverride("margin_right", Spacing6);
        rootMargin.AddThemeConstantOverride("margin_top", Spacing4);
        rootMargin.AddThemeConstantOverride("margin_bottom", Spacing4);
        AddChild(rootMargin);

        var mainVbox = new VBoxContainer();
        mainVbox.AddThemeConstantOverride("separation", Spacing4);
        rootMargin.AddChild(mainVbox);

        // 标题栏
        BuildHeader(mainVbox);

        // 网格容器
        BuildGridArea(mainVbox);

        // 底部信息栏
        BuildFooter(mainVbox);

        // Tooltip（浮动层）
        BuildTooltip();
    }

    private void BuildHeader(VBoxContainer parent)
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", Spacing4);
        parent.AddChild(header);

        var title = new Label();
        title.Text = "背包";
        title.AddThemeFontSizeOverride("font_size", FontLarge);
        title.AddThemeColorOverride("font_color", TextAccent);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        _capacityLabel = new Label();
        _capacityLabel.Text = "容量: 0/0";
        _capacityLabel.AddThemeFontSizeOverride("font_size", FontSmall);
        _capacityLabel.AddThemeColorOverride("font_color", TextSecondary);
        header.AddChild(_capacityLabel);

        // 整理按钮
        var sortBtn = CreateButton("整理", 60);
        sortBtn.Pressed += OnSortPressed;
        header.AddChild(sortBtn);

        // 关闭按钮
        var closeBtn = CreateButton("✕", 28);
        closeBtn.Pressed += () =>
        {
            Visible = false;
            EmitSignal(SignalName.CloseRequested);
        };
        header.AddChild(closeBtn);
    }

    private void BuildGridArea(VBoxContainer parent)
    {
        var gridPanel = new PanelContainer();
        var gridStyle = new StyleBoxFlat { BgColor = BgGrid };
        gridStyle.SetBorderWidthAll(1);
        gridStyle.BorderColor = BorderDefault;
        gridStyle.SetCornerRadiusAll(3);
        gridStyle.SetContentMarginAll(4);
        gridPanel.AddThemeStyleboxOverride("panel", gridStyle);
        gridPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        parent.AddChild(gridPanel);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        gridPanel.AddChild(scroll);

        _gridContainer = new Control();
        _gridContainer.MouseFilter = MouseFilterEnum.Stop;
        scroll.AddChild(_gridContainer);

        // 网格输入处理
        _gridContainer.GuiInput += OnGridInput;
    }

    private void BuildFooter(VBoxContainer parent)
    {
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", Spacing4);
        parent.AddChild(footer);

        _weightLabel = new Label();
        _weightLabel.Text = "物品: 0件 | 价值: 0金";
        _weightLabel.AddThemeFontSizeOverride("font_size", FontTiny);
        _weightLabel.AddThemeColorOverride("font_color", TextMuted);
        _weightLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_weightLabel);

        var helpLabel = new Label();
        helpLabel.Text = "左键拖拽移动 | 右键使用/查看";
        helpLabel.AddThemeFontSizeOverride("font_size", FontTiny);
        helpLabel.AddThemeColorOverride("font_color", TextMuted);
        footer.AddChild(helpLabel);
    }

    private void BuildTooltip()
    {
        _tooltipPanel = new PanelContainer();
        _tooltipPanel.Visible = false;
        _tooltipPanel.ZIndex = 100;
        _tooltipPanel.MouseFilter = MouseFilterEnum.Ignore;

        var ttStyle = new StyleBoxFlat();
        ttStyle.BgColor = new Color(0.05f, 0.05f, 0.07f, 0.95f);
        ttStyle.SetBorderWidthAll(1);
        ttStyle.BorderColor = BorderHighlight;
        ttStyle.SetCornerRadiusAll(3);
        ttStyle.SetContentMarginAll(8);
        ((PanelContainer)_tooltipPanel).AddThemeStyleboxOverride("panel", ttStyle);

        _tooltipText = new RichTextLabel();
        _tooltipText.BbcodeEnabled = true;
        _tooltipText.ScrollActive = false;
        _tooltipText.FitContent = true;
        _tooltipText.CustomMinimumSize = new Vector2(200, 0);
        _tooltipText.MouseFilter = MouseFilterEnum.Ignore;
        _tooltipPanel.AddChild(_tooltipText);

        AddChild(_tooltipPanel);
    }

    // ============================================================================
    // 网格构建
    // ============================================================================

    private void RebuildGrid()
    {
        // 清除旧格子
        foreach (var cell in _cellPanels.Values)
            cell.QueueFree();
        _cellPanels.Clear();
        ClearItemControls();

        if (_inventory == null) return;

        int totalW = _inventory.GridWidth * (CellSize + CellGap) - CellGap;
        int totalH = _inventory.GridHeight * (CellSize + CellGap) - CellGap;
        _gridContainer.CustomMinimumSize = new Vector2(totalW, totalH);

        // 创建格子背景
        for (int y = 0; y < _inventory.GridHeight; y++)
        {
            for (int x = 0; x < _inventory.GridWidth; x++)
            {
                var cell = new Panel();
                cell.Position = new Vector2(x * (CellSize + CellGap), y * (CellSize + CellGap));
                cell.Size = new Vector2(CellSize, CellSize);
                cell.MouseFilter = MouseFilterEnum.Ignore;

                var cellStyle = new StyleBoxFlat { BgColor = BgCell };
                cellStyle.SetBorderWidthAll(1);
                cellStyle.BorderColor = BorderDefault;
                cellStyle.SetCornerRadiusAll(1);
                cell.AddThemeStyleboxOverride("panel", cellStyle);

                _gridContainer.AddChild(cell);
                _cellPanels[new Vector2I(x, y)] = cell;
            }
        }

        // 更新面板尺寸
        int panelW = totalW + Spacing6 * 2 + 8 + 4; // margins + border + padding
        int panelH = totalH + 100 + Spacing4 * 2 + Spacing6 * 2; // header + footer + margins
        CustomMinimumSize = new Vector2(panelW, panelH);
    }

    // ============================================================================
    // 物品控件
    // ============================================================================

    private void CreateItemControl(GridItem gridItem)
    {
        var control = new Panel();
        control.Position = new Vector2(
            gridItem.GridX * (CellSize + CellGap),
            gridItem.GridY * (CellSize + CellGap));
        control.Size = new Vector2(
            gridItem.Width * (CellSize + CellGap) - CellGap,
            gridItem.Height * (CellSize + CellGap) - CellGap);
        control.MouseFilter = MouseFilterEnum.Stop;
        control.ZIndex = 1;

        // 根据稀有度设置样式
        var itemStyle = new StyleBoxFlat { BgColor = BgItemNormal };
        itemStyle.SetBorderWidthAll(1);
        itemStyle.BorderColor = gridItem.Item.GetRarityColor() * 0.8f;
        itemStyle.SetCornerRadiusAll(2);
        control.AddThemeStyleboxOverride("panel", itemStyle);

        // 物品图标
        var iconRect = new TextureRect();
        iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        iconRect.OffsetLeft = 3;
        iconRect.OffsetTop = 3;
        iconRect.OffsetRight = -3;
        iconRect.OffsetBottom = -3;
        iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        control.AddChild(iconRect);

        // 尝试加载图标
        if (!string.IsNullOrEmpty(gridItem.Item.IconId))
        {
            var tex = GD.Load<Texture2D>(gridItem.Item.IconId);
            if (tex != null) iconRect.Texture = tex;
        }

        // 物品名称（小字）
        var nameLabel = new Label();
        nameLabel.Text = gridItem.Item.ItemName.Length > 4
            ? gridItem.Item.ItemName[..4]
            : gridItem.Item.ItemName;
        nameLabel.AddThemeFontSizeOverride("font_size", FontTiny);
        nameLabel.AddThemeColorOverride("font_color", gridItem.Item.GetRarityColor());
        nameLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
        nameLabel.OffsetLeft = 2;
        nameLabel.OffsetBottom = -1;
        nameLabel.MouseFilter = MouseFilterEnum.Ignore;
        control.AddChild(nameLabel);

        // 堆叠数量
        if (gridItem.Quantity > 1)
        {
            var qtyLabel = new Label();
            qtyLabel.Text = gridItem.Quantity.ToString();
            qtyLabel.AddThemeFontSizeOverride("font_size", FontSmall);
            qtyLabel.AddThemeColorOverride("font_color", TextPrimary);
            qtyLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
            qtyLabel.OffsetRight = -3;
            qtyLabel.OffsetTop = 1;
            qtyLabel.MouseFilter = MouseFilterEnum.Ignore;
            control.AddChild(qtyLabel);
        }

        // 输入事件
        control.GuiInput += (ev) => OnItemInput(ev, gridItem, control);
        control.MouseEntered += () => ShowItemTooltip(gridItem, control);
        control.MouseExited += () => HideTooltip();

        control.SetMeta("instance_id", gridItem.InstanceId);
        _gridContainer.AddChild(control);
        _itemControls[gridItem.InstanceId] = control;
    }

    private void ClearItemControls()
    {
        foreach (var ctrl in _itemControls.Values)
            ctrl.QueueFree();
        _itemControls.Clear();
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
                    EmitSignal(SignalName.ItemDoubleClicked, gridItem.InstanceId);
                    return;
                }
                StartDrag(gridItem, control, mouseBtn.GlobalPosition);
            }
            else if (mouseBtn.ButtonIndex == MouseButton.Right && mouseBtn.Pressed)
            {
                EmitSignal(SignalName.ItemRightClicked, gridItem.InstanceId);
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
        _dragGhost.ZIndex = 50;
        _dragGhost.MouseFilter = MouseFilterEnum.Ignore;
        _dragGhost.Modulate = new Color(1, 1, 1, 0.7f);

        var ghostStyle = new StyleBoxFlat { BgColor = new Color(0.15f, 0.13f, 0.2f, 0.8f) };
        ghostStyle.SetBorderWidthAll(2);
        ghostStyle.BorderColor = BorderDrag;
        ghostStyle.SetCornerRadiusAll(2);
        _dragGhost.AddThemeStyleboxOverride("panel", ghostStyle);

        // 幽灵中显示物品名
        var ghostLabel = new Label();
        ghostLabel.Text = gridItem.Item.ItemName;
        ghostLabel.AddThemeFontSizeOverride("font_size", FontSmall);
        ghostLabel.AddThemeColorOverride("font_color", gridItem.Item.GetRarityColor());
        ghostLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        ghostLabel.HorizontalAlignment = HorizontalAlignment.Center;
        ghostLabel.MouseFilter = MouseFilterEnum.Ignore;
        _dragGhost.AddChild(ghostLabel);

        AddChild(_dragGhost);
        _dragGhost.GlobalPosition = mousePos + _dragOffset;

        // 半透明原位物品
        control.Modulate = new Color(1, 1, 1, 0.3f);

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

        // 恢复原物品透明度
        if (_draggedItem != null && _itemControls.TryGetValue(_draggedItem.InstanceId, out var ctrl))
            ctrl.Modulate = Colors.White;

        _draggedItem = null;
        ClearCellHighlights();
    }

    private void CompleteDrag(int targetX, int targetY)
    {
        if (_inventory == null || _draggedItem == null) return;

        // 检查目标位置是否有其他物品
        var targetItem = _inventory.GetItemAt(targetX, targetY);

        bool success;
        if (targetItem != null && targetItem != _draggedItem)
        {
            // 尝试交换
            success = _inventory.TrySwap(_draggedItem, targetItem);
        }
        else
        {
            // 尝试移动
            success = _inventory.TryMove(_draggedItem, targetX, targetY);
        }

        if (success)
        {
            EmitSignal(SignalName.ItemDropped, _draggedItem.InstanceId, targetX, targetY);
        }

        CancelDrag();
        RefreshItems();
    }

    // ============================================================================
    // 输入处理
    // ============================================================================

    private void OnGridInput(InputEvent ev)
    {
        if (ev is InputEventMouseMotion motion)
        {
            UpdateHoverCell(motion.Position);

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
                // 释放拖拽
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
        {
            _dragGhost.GlobalPosition = motion.GlobalPosition + _dragOffset;
        }
        else if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false })
        {
            // 在面板外释放 = 取消
            CancelDrag();
        }
        else if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            CancelDrag();
        }
    }

    // ============================================================================
    // 格子高亮
    // ============================================================================

    private void UpdateHoverCell(Vector2 localPos)
    {
        var cell = PositionToCell(localPos);
        _hoverCellX = cell.X;
        _hoverCellY = cell.Y;
    }

    private void UpdateDragHighlight()
    {
        ClearCellHighlights();
        if (_inventory == null || _draggedItem == null) return;
        if (_hoverCellX < 0 || _hoverCellY < 0) return;

        int w = _draggedItem.Width;
        int h = _draggedItem.Height;

        // 临时移除拖拽物品的占用来检查
        bool canPlace = _inventory.CanPlaceSize(w, h, _hoverCellX, _hoverCellY, _draggedItem);
        Color highlightColor = canPlace ? BgCellValid : BgCellInvalid;

        for (int dx = 0; dx < w; dx++)
        {
            for (int dy = 0; dy < h; dy++)
            {
                int cx = _hoverCellX + dx;
                int cy = _hoverCellY + dy;
                var key = new Vector2I(cx, cy);
                if (_cellPanels.TryGetValue(key, out var cellPanel))
                {
                    var style = new StyleBoxFlat { BgColor = highlightColor };
                    style.SetBorderWidthAll(1);
                    style.BorderColor = canPlace
                        ? new Color(0.3f, 0.8f, 0.3f, 0.8f)
                        : new Color(0.8f, 0.3f, 0.3f, 0.8f);
                    style.SetCornerRadiusAll(1);
                    cellPanel.AddThemeStyleboxOverride("panel", style);
                }
            }
        }
    }

    private void ClearCellHighlights()
    {
        foreach (var kvp in _cellPanels)
        {
            var style = new StyleBoxFlat { BgColor = BgCell };
            style.SetBorderWidthAll(1);
            style.BorderColor = BorderDefault;
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
        var rarityColor = item.GetRarityColor().ToHtml(false);
        string text = $"[color=#{rarityColor}][b]{item.GetFullName()}[/b][/color]\n";
        text += $"[color=#aaa]{item.GetRarityName()}[/color]\n";

        if (item is WeaponData weapon)
        {
            text += $"\n[color=#ccc]{weapon.GetWeaponDescription()}[/color]";
        }
        else if (item is ArmorData armor)
        {
            text += $"\n[color=#ccc]装甲: {armor.MaxArmorPoints} | 阈值: {armor.DrThreshold}[/color]";
            if (armor.MaxDexBonus < 99)
                text += $"\n[color=#ccc]DEX上限: {armor.MaxDexBonus}[/color]";
        }

        if (!string.IsNullOrEmpty(item.Description))
            text += $"\n\n[color=#888]{item.Description}[/color]";

        text += $"\n\n[color=#666]占用: {item.InvWidth}×{item.InvHeight} 格[/color]";
        text += $"\n[color=#666]价值: {item.GetSellPrice()} 金[/color]";

        if (gridItem.Quantity > 1)
            text += $"\n[color=#666]数量: {gridItem.Quantity}[/color]";

        string affixDesc = item.GetAffixDescriptions();
        if (!string.IsNullOrEmpty(affixDesc))
            text += $"\n\n[color=#9b59b6]{affixDesc}[/color]";

        _tooltipText.Text = text;
        _tooltipPanel.Visible = true;

        // 定位tooltip在物品右侧
        var ttPos = source.GlobalPosition + new Vector2(source.Size.X + 8, 0);
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

        if (_inventory == null) return new Vector2I(-1, -1);
        if (x < 0 || x >= _inventory.GridWidth || y < 0 || y >= _inventory.GridHeight)
            return new Vector2I(-1, -1);

        return new Vector2I(x, y);
    }

    private void UpdateCapacityDisplay()
    {
        if (_inventory == null) return;

        int used = _inventory.UsedCells;
        int total = _inventory.TotalCells;
        _capacityLabel.Text = $"容量: {used}/{total}";

        _weightLabel.Text = $"物品: {_inventory.TotalItemCount}件 | 价值: {_inventory.TotalValue}金";
    }

    private void OnSortPressed()
    {
        _inventory?.AutoSort();
        RefreshItems();
        EmitSignal(SignalName.SortRequested);
    }

    private Button CreateButton(string text, int width)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(width, 24);
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
}
