// ShopGridView.cs
// 商店/战利品网格视图 — 容量无限的物品列表，支持卖出/拾取
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Inventory;

/// <summary>
/// 商店/战利品视图。
/// - 持有 List&lt;ItemData&gt; 货架（无固定容量）
/// - 内部仍用 GridInventory 做布局排列（高度按需扩展）
/// - 商店模式：CanAccept = 玩家有钱；Accept = 扣钱
/// - 战利品模式：CanAccept = true；Accept = 直接拾取
/// </summary>
[GlobalClass]
public partial class ShopGridView : Control, IItemContainer
{
    public const int CellSize = 42;
    public const int CellGap = 2;
    public const int GridWidth = 10;

    private static readonly Color BgCell = new(0.08f, 0.08f, 0.10f, 0.85f);

    private List<ItemData>? _stock;
    private GridInventory _layout = new();
    private DragController? _dragController;
    private ItemPopup? _popup;
    private readonly Dictionary<Vector2I, Panel> _cellPanels = new();
    private readonly Dictionary<string, Control> _itemControls = new();

    /// <summary>商店经济（null = 战利品模式，免费拾取）</summary>
    public EconomyManager? Economy { get; set; }

    /// <summary>繁荣度（影响价格，仅商店模式）</summary>
    public int Prosperity { get; set; } = 50;

    /// <summary>当玩家从其他容器拖入此区域时的回调（用于卖出/丢弃）</summary>
    public Action<DragSource>? OnItemDroppedFromOutside { get; set; }

    /// <summary>金币变化回调（用于刷新外部 UI 标签）</summary>
    public Action<int>? OnGoldChanged { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void Initialize(List<ItemData> stock, DragController dragCtrl, ItemPopup popup)
    {
        _stock = stock;
        _dragController = dragCtrl;
        _popup = popup;
        _dragController?.RegisterContainer(this);
        Rebuild();
    }

    public List<ItemData>? Stock => _stock;

    public int GetBuyPrice(ItemData item)
    {
        int basePrice = item.Price;
        float markup = 1.0f + (100 - Prosperity) * 0.005f;
        return Math.Max(1, Mathf.RoundToInt(basePrice * markup));
    }

    /// <summary>重建布局并渲染</summary>
    public void Rebuild()
    {
        foreach (var p in _cellPanels.Values) p.QueueFree();
        _cellPanels.Clear();
        foreach (var c in _itemControls.Values) c.QueueFree();
        _itemControls.Clear();

        if (_stock == null) return;

        // 应用尺寸
        foreach (var item in _stock) ItemSizeConfig.ApplyRecommendedSize(item);

        // 按需计算高度（无上限）
        int totalArea = _stock.Sum(i => i.InvWidth * i.InvHeight);
        int maxItemH = _stock.Count > 0 ? _stock.Max(i => i.InvHeight) : 1;
        int height = Math.Max(maxItemH + 1, (int)Math.Ceiling((double)totalArea / GridWidth) * 2 + maxItemH);

        _layout = new GridInventory();
        _layout.SetGridSize(GridWidth, height);

        // 自动放置（按面积降序）
        foreach (var item in _stock.OrderByDescending(i => i.InvWidth * i.InvHeight))
            _layout.TryAutoPlace(item);
        _layout.AutoSort();

        int totalW = GridWidth * (CellSize + CellGap) - CellGap;
        int totalH = height * (CellSize + CellGap) - CellGap;
        CustomMinimumSize = new Vector2(totalW, totalH);

        // 格子
        for (int y = 0; y < height; y++)
        for (int x = 0; x < GridWidth; x++)
        {
            var cell = MakeCellPanel(x, y);
            _cellPanels[new Vector2I(x, y)] = cell;
            AddChild(cell);
        }

        // 物品
        foreach (var gi in _layout.Items)
            CreateItemControl(gi);
    }

    public void Refresh() => Rebuild();

    private Panel MakeCellPanel(int x, int y)
    {
        var cell = new Panel();
        cell.Position = new Vector2(x * (CellSize + CellGap), y * (CellSize + CellGap));
        cell.Size = new Vector2(CellSize, CellSize);
        cell.MouseFilter = MouseFilterEnum.Ignore;

        bool alt = (x + y) % 2 == 1;
        var bg = alt ? new Color(BgCell.R + 0.012f, BgCell.G + 0.012f, BgCell.B + 0.015f, BgCell.A) : BgCell;
        ApplyCellStyle(cell, bg, new Color(0.25f, 0.22f, 0.18f, 0.4f));
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
        string overlay = Economy != null ? $"{GetBuyPrice(gi.Item)}金" : "拾取";
        var color = Economy != null ? new Color(0.3f, 0.85f, 0.3f) : new Color(0.9f, 0.8f, 0.5f);
        var w = ItemGridWidget.Create(gi.Item, CellSize, CellGap, 1, overlay, color);
        w.Position = new Vector2(gi.GridX * (CellSize + CellGap), gi.GridY * (CellSize + CellGap));

        w.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    var src = new DragSource { Container = this, Item = gi.Item, Origin = gi.Item };
                    _dragController?.BeginDrag(src, mb.GlobalPosition, w.GlobalPosition, w.Size);
                    GetViewport().SetInputAsHandled();
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    _popup?.ShowFor(gi.Item, mb.GlobalPosition);
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
        if (!IsInsideTree()) return null;
        var rect = GetGlobalRect();
        if (!rect.HasPoint(globalMousePos)) return null;
        // 商店区域不需要精确格子（玩家随手丢）
        return new ContainerHitInfo { Container = this, Target = null };
    }

    public bool CanAccept(DragSource source, ContainerHitInfo hit)
    {
        // 商店区域不接受自己内部的拖拽（来自背包/装备的才有效）
        if (source.Container == this) return false;
        return true;
    }

    public bool Accept(DragSource source, ContainerHitInfo hit)
    {
        if (_stock == null) return false;
        // 调用外部回调处理卖出/丢弃语义
        OnItemDroppedFromOutside?.Invoke(source);
        // 商店模式：卖出 → 加金币 + 入货架
        if (Economy != null)
        {
            int sellPrice = source.Item.GetSellPrice();
            Economy.Gold += sellPrice;
            OnGoldChanged?.Invoke(Economy.Gold);
        }
        _stock.Add(source.Item);
        return true;
    }

    public void RemoveFromSource(DragSource source)
    {
        // 商店物品被拖到背包：从货架移除 + 扣钱
        if (_stock != null && _stock.Contains(source.Item))
            _stock.Remove(source.Item);
        if (Economy != null)
        {
            int price = GetBuyPrice(source.Item);
            Economy.Gold -= price;
            OnGoldChanged?.Invoke(Economy.Gold);
        }
    }

    public void HighlightDropTarget(DragSource source, ContainerHitInfo? hit)
    {
        // 商店区域统一高亮整片绿色
        ClearHighlights();
        if (hit?.Container != this) return;
        var border = new Color(0.5f, 0.85f, 0.3f, 0.7f);
        foreach (var p in _cellPanels.Values)
        {
            var s = new StyleBoxFlat { BgColor = new Color(0.1f, 0.18f, 0.08f, 0.6f) };
            s.SetBorderWidthAll(1);
            s.BorderColor = border;
            s.SetCornerRadiusAll(1);
            p.AddThemeStyleboxOverride("panel", s);
        }
    }

    public void ClearHighlights()
    {
        foreach (var kvp in _cellPanels)
        {
            bool alt = (kvp.Key.X + kvp.Key.Y) % 2 == 1;
            var bg = alt ? new Color(BgCell.R + 0.012f, BgCell.G + 0.012f, BgCell.B + 0.015f, BgCell.A) : BgCell;
            ApplyCellStyle(kvp.Value, bg, new Color(0.25f, 0.22f, 0.18f, 0.4f));
        }
    }
}
