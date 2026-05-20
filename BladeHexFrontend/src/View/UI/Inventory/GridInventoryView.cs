// GridInventoryView.cs
// 网格背包容器视图 — 实现 IItemContainer
// 负责：渲染网格 + 物品 + 接收/移除/高亮逻辑
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Inventory;

/// <summary>
/// 网格背包视图。挂载到 ScrollContainer 内部，负责：
/// - 绘制格子背景
/// - 渲染物品（使用 ItemGridWidget）
/// - 实现 IItemContainer：HitTest / CanAccept / Accept / RemoveFromSource
/// </summary>
[GlobalClass]
public partial class GridInventoryView : Control, IItemContainer
{
    [Signal] public delegate void ItemRightClickedEventHandler();

    public const int CellSize = 72;
    public const int CellGap = 3;

    private static readonly Color BgCell = new(0.08f, 0.08f, 0.10f, 0.85f);
    private static readonly Color BgCellValid = new(0.08f, 0.22f, 0.08f, 0.7f);
    private static readonly Color BgCellInvalid = new(0.28f, 0.06f, 0.06f, 0.7f);

    private GridInventory? _inventory;
    private DragController? _dragController;
    private ItemPopup? _popup;
    private readonly Dictionary<Vector2I, Panel> _cellPanels = new();
    private readonly Dictionary<string, Control> _itemControls = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
    }

    /// <summary>初始化（绑定数据 + 拖拽控制器 + 详情弹窗）</summary>
    public void Initialize(GridInventory inventory, DragController dragCtrl, ItemPopup popup)
    {
        _inventory = inventory;
        _dragController = dragCtrl;
        _popup = popup;
        _dragController?.RegisterContainer(this);
        Rebuild();
    }

    /// <summary>更新背包数据引用（不触发完整重建）</summary>
    public void SetInventory(GridInventory inventory)
    {
        _inventory = inventory;
        Rebuild();
    }

    public GridInventory? Inventory => _inventory;

    /// <summary>从头重建网格和物品</summary>
    public void Rebuild()
    {
        foreach (var p in _cellPanels.Values) p.QueueFree();
        _cellPanels.Clear();
        foreach (var c in _itemControls.Values) c.QueueFree();
        _itemControls.Clear();

        if (_inventory == null) return;

        int gw = _inventory.GridWidth;
        int gh = _inventory.GridHeight;
        int totalW = gw * (CellSize + CellGap) - CellGap;
        int totalH = gh * (CellSize + CellGap) - CellGap;
        CustomMinimumSize = new Vector2(totalW, totalH);

        // 格子背景
        for (int y = 0; y < gh; y++)
        for (int x = 0; x < gw; x++)
        {
            var cell = MakeCellPanel(x, y);
            _cellPanels[new Vector2I(x, y)] = cell;
            AddChild(cell);
        }

        // 物品控件
        foreach (var gi in _inventory.Items)
            CreateItemControl(gi);
    }

    /// <summary>仅刷新物品（保留格子）</summary>
    public void Refresh()
    {
        foreach (var c in _itemControls.Values) c.QueueFree();
        _itemControls.Clear();

        if (_inventory == null) return;

        foreach (var gi in _inventory.Items)
            CreateItemControl(gi);
    }

    private Panel MakeCellPanel(int x, int y)
    {
        var cell = new Panel();
        cell.Position = new Vector2(x * (CellSize + CellGap), y * (CellSize + CellGap));
        cell.Size = new Vector2(CellSize, CellSize);
        cell.MouseFilter = MouseFilterEnum.Ignore;

        bool alt = (x + y) % 2 == 1;
        var bg = alt ? new Color(BgCell.R + 0.012f, BgCell.G + 0.012f, BgCell.B + 0.015f, BgCell.A) : BgCell;
        ApplyCellStyle(cell, bg, new Color(0.18f, 0.18f, 0.2f, 0.4f));
        return cell;
    }

    private static void ApplyCellStyle(Panel cell, Color bg, Color border)
    {
        var s = new StyleBoxFlat { BgColor = bg };
        s.SetBorderWidthAll(1);
        s.BorderColor = border;
        s.SetCornerRadiusAll(1);
        cell.AddThemeStyleboxOverride("panel", s);
    }

    private void CreateItemControl(GridItem gi)
    {
        var w = ItemGridWidget.Create(gi.Item, CellSize, CellGap, gi.Quantity);
        w.Position = new Vector2(gi.GridX * (CellSize + CellGap), gi.GridY * (CellSize + CellGap));

        w.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    var src = new DragSource { Container = this, Item = gi.Item, Origin = gi };
                    _dragController?.BeginDrag(src, mb.GlobalPosition, w.GlobalPosition, w.Size);
                    GetViewport().SetInputAsHandled();
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    _popup?.ShowFor(gi.Item, mb.GlobalPosition);
                    EmitSignal(SignalName.ItemRightClicked);
                    GetViewport().SetInputAsHandled();
                }
            }
        };

        AddChild(w);
        _itemControls[gi.InstanceId] = w;
    }

    // ============================================================================
    // IItemContainer
    // ============================================================================

    public new Rect2 GetGlobalRect() => base.GetGlobalRect();

    public ContainerHitInfo? HitTest(Vector2 globalMousePos)
    {
        if (!IsInsideTree() || _inventory == null) return null;
        var rect = GetGlobalRect();
        if (!rect.HasPoint(globalMousePos)) return null;

        var local = globalMousePos - rect.Position;
        int x = (int)(local.X / (CellSize + CellGap));
        int y = (int)(local.Y / (CellSize + CellGap));
        if (x < 0 || x >= _inventory.GridWidth || y < 0 || y >= _inventory.GridHeight) return null;

        return new ContainerHitInfo { Container = this, Target = new Vector2I(x, y) };
    }

    public bool CanAccept(DragSource source, ContainerHitInfo hit)
    {
        if (_inventory == null || hit.Target is not Vector2I cell) return false;
        if (source.Container is ShopGridView shop && !shop.CanPurchase(source.Item))
            return false;

        var target = _inventory.GetItemAt(cell.X, cell.Y);
        if (target != null)
        {
            if (source.Origin is GridItem dragged && dragged == target)
                return _inventory.CanPlaceSize(source.Item.InvWidth, source.Item.InvHeight, cell.X, cell.Y, dragged);
            if (_inventory.CanStack(source.Item, target.Item))
                return true;
        }

        var ignore = source.Origin as GridItem;
        return _inventory.CanPlaceSize(source.Item.InvWidth, source.Item.InvHeight, cell.X, cell.Y, ignore);
    }

    public bool Accept(DragSource source, ContainerHitInfo hit)
    {
        if (_inventory == null || hit.Target is not Vector2I cell) return false;

        // 同一容器内移动 = TryMove/TrySwap
        if (source.Container == this && source.Origin is GridItem dragged)
        {
            var existing = _inventory.GetItemAt(cell.X, cell.Y);
            if (existing != null && existing != dragged)
            {
                if (_inventory.CanStack(dragged.Item, existing.Item))
                    return _inventory.TryMerge(dragged, existing);
                return _inventory.TrySwap(dragged, existing);
            }
            return _inventory.TryMove(dragged, cell.X, cell.Y);
        }

        // 来自其他容器：克隆物品避免共享引用（商店连续购买同款不会共享同一实例）
        var itemToPlace = source.Item.Duplicate(true) as ItemData ?? source.Item;
        if (_inventory.TryStackAt(itemToPlace, cell.X, cell.Y))
            return true;
        return _inventory.TryPlace(itemToPlace, cell.X, cell.Y);
    }

    public void RemoveFromSource(DragSource source)
    {
        if (_inventory == null) return;
        // Origin 是背包内的 GridItem 引用，从背包真实移除
        if (source.Origin is GridItem gi)
            _inventory.Remove(gi);
    }

    public void HighlightDropTarget(DragSource source, ContainerHitInfo? hit)
    {
        ClearHighlights();
        if (hit == null || hit.Container != this || hit.Target is not Vector2I cell) return;
        if (_inventory == null) return;

        var ignore = source.Origin as GridItem;
        var target = _inventory.GetItemAt(cell.X, cell.Y);
        bool ok = false;
        if (target != null)
        {
            bool sameDragged = source.Origin is GridItem dragged && dragged == target;
            ok = !sameDragged && _inventory.CanStack(source.Item, target.Item);
        }
        if (!ok)
            ok = _inventory.CanPlaceSize(source.Item.InvWidth, source.Item.InvHeight, cell.X, cell.Y, ignore);
        var bg = ok ? BgCellValid : BgCellInvalid;
        var border = ok ? new Color(0.25f, 0.7f, 0.25f, 0.85f) : new Color(0.7f, 0.2f, 0.2f, 0.85f);

        for (int dx = 0; dx < source.Item.InvWidth; dx++)
        for (int dy = 0; dy < source.Item.InvHeight; dy++)
        {
            var key = new Vector2I(cell.X + dx, cell.Y + dy);
            if (_cellPanels.TryGetValue(key, out var p))
                ApplyCellStyle(p, bg, border);
        }
    }

    public void ClearHighlights()
    {
        foreach (var kvp in _cellPanels)
        {
            bool alt = (kvp.Key.X + kvp.Key.Y) % 2 == 1;
            var bg = alt ? new Color(BgCell.R + 0.012f, BgCell.G + 0.012f, BgCell.B + 0.015f, BgCell.A) : BgCell;
            ApplyCellStyle(kvp.Value, bg, new Color(0.18f, 0.18f, 0.2f, 0.4f));
        }
    }
}
