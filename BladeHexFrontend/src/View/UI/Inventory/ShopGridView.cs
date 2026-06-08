// ShopGridView.cs
// 商店/战利品网格视图 — 容量无限的物品列表，支持卖出/拾取
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;
using BladeHex.Strategic.WorldEvents;

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
    public const int CellSize = 72;
    public const int CellGap = 3;
    public const int GridWidth = 10;

    private static readonly Color BgCell = new(0.08f, 0.08f, 0.10f, 0.85f);

    private List<ItemData>? _stock;
    private GridInventory _layout = new();
    private DragController? _dragController;
    private ItemPopup? _popup;
    private readonly Dictionary<Vector2I, Panel> _cellPanels = new();
    private readonly Dictionary<string, Control> _itemControls = new();
    private readonly Dictionary<string, int> _stockQuantities = new();

    /// <summary>商店经济（null = 战利品模式，免费拾取）</summary>
    public EconomyManager? Economy { get; set; }

    /// <summary>繁荣度（影响价格，仅商店模式）</summary>
    public int Prosperity { get; set; } = 50;

    public OverworldPOI? Poi { get; set; }
    public EconomyEventEngine? EventEngine { get; set; }
    public ReputationTracker? Reputation { get; set; }
    public WorldEventEngine? WorldEngine { get; set; }
    public int PlayerLevel { get; set; } = 1;

    /// <summary>是否为敌国城镇（用于走私功能）</summary>
    public bool IsEnemyTown { get; set; } = false;

    /// <summary>当玩家从其他容器拖入此区域时的回调（用于卖出/丢弃）</summary>
    public Action<DragSource>? OnItemDroppedFromOutside { get; set; }

    /// <summary>金币变化回调（用于刷新外部 UI 标签）</summary>
    public Action<int>? OnGoldChanged { get; set; }

    /// <summary>走私结果回调</summary>
    public Action<string>? OnSmuggleResult { get; set; }

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

    private string GetItemCategory(ItemData item)
    {
        if (item == null) return "all";
        if (item.ItemId.Contains("horse") || item.ItemName.Contains("马")) return "horse";
        if (item.ItemId == "rations" || item.ItemId == "beer" || item.ItemId == "bandage" || item.ItemId.Contains("food") || item.ItemName.Contains("口粮") || item.ItemName.Contains("麦") || item.ItemName.Contains("酒")) return "food";
        if (item.EquipSlotTarget == ItemData.EquipSlot.Weapon || item.EquipSlotTarget == ItemData.EquipSlot.Helmet || item.ItemId.Contains("sword") || item.ItemId.Contains("shield") || item.ItemId.Contains("armor") || item.ItemId.Contains("bow") || item.ItemId.Contains("boots")) return "weapon";
        return "all";
    }

    public int GetBuyPrice(ItemData item)
    {
        return TradePricingService.GetBuyPrice(item, Prosperity, 1.0f, null, Poi, EventEngine);
    }

    public int GetSellPrice(ItemData item)
    {
        return TradePricingService.GetSellPrice(item, Prosperity, 1.0f, null, Poi, EventEngine);
    }

    public int GetSmuggleBuyPrice(ItemData item)
    {
        return (int)Math.Round(TradePricingService.GetBuyPrice(item, Prosperity) * 0.8);
    }

    public int GetSmuggleSellPrice(ItemData item)
    {
        return (int)Math.Round(TradePricingService.GetSellPrice(item, Prosperity) * 1.1);
    }

    public bool CanPurchase(ItemData item)
    {
        if (Economy == null) return true; // 战利品模式：免费拾取
        return Economy.Gold >= GetBuyPrice(item);
    }

    /// <summary>检查是否可以走私（敌国城镇 + 声望≤-50 或 金币≥5000）</summary>
    public bool CanSmuggle()
    {
        if (!IsEnemyTown || Economy == null || Poi == null || Reputation == null) return false;
        int rep = Reputation.GetReputation(Poi.OwningFaction);
        return rep <= -50 || Economy.Gold >= 5000;
    }

    /// <summary>尝试走私购入</summary>
    public bool TrySmuggleBuy(ItemData item, int qty)
    {
        if (Economy == null || Poi == null || Reputation == null) return false;

        var result = SmugglingService.TryBuySmuggle(
            item, qty, Poi, PlayerLevel, Economy.DaysPassed,
            Economy, WorldEngine, Reputation);

        if (result.Success)
        {
            OnSmuggleResult?.Invoke($"走私成功！购入 {qty}x {item.ItemName}，花费 {-result.GoldDelta} 金币");
            OnGoldChanged?.Invoke(Economy.Gold);
            return true;
        }
        else
        {
            OnSmuggleResult?.Invoke($"[color=red]走私失败！{result.FailReason}[/color]");
            OnGoldChanged?.Invoke(Economy.Gold);
            return false;
        }
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
        var displayStock = BuildDisplayStock();

        // 按需计算高度（无上限）
        int totalArea = displayStock.Sum(i => i.InvWidth * i.InvHeight);
        int maxItemH = displayStock.Count > 0 ? displayStock.Max(i => i.InvHeight) : 1;
        int height = Math.Max(maxItemH + 1, (int)Math.Ceiling((double)totalArea / GridWidth) * 2 + maxItemH);

        _layout = new GridInventory();
        _layout.SetGridSize(GridWidth, height);

        // 自动放置（按面积降序）
        foreach (var item in displayStock.OrderByDescending(i => i.InvWidth * i.InvHeight))
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

    private List<ItemData> BuildDisplayStock()
    {
        _stockQuantities.Clear();
        if (_stock == null) return new List<ItemData>();

        var display = new List<ItemData>();
        foreach (var item in _stock)
        {
            string key = GetStockKey(item);
            if (IsStackableForDisplay(item) && _stockQuantities.ContainsKey(key))
            {
                _stockQuantities[key]++;
                continue;
            }

            if (IsStackableForDisplay(item)) _stockQuantities[key] = 1;
            else _stockQuantities[$"{key}|{display.Count}"] = 1;
            display.Add(item);
        }

        return display;
    }

    private static string GetStockKey(ItemData item) => $"{item.ItemId}|{item.ItemName}";

    private static bool IsStackableForDisplay(ItemData item)
    {
        return item is ConsumableData || item.SourceTags.Contains("material") || item.SourceTags.Contains("supply");
    }

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
        int quantity = IsStackableForDisplay(gi.Item) && _stockQuantities.TryGetValue(GetStockKey(gi.Item), out int qty) ? qty : gi.Quantity;
        string overlay = Economy != null ? $"{GetBuyPrice(gi.Item)}金" : "拾取";
        var color = Economy != null ? new Color(0.3f, 0.85f, 0.3f) : new Color(0.9f, 0.8f, 0.5f);
        
        if (Economy != null && Poi != null && EventEngine != null)
        {
            float m = EventEngine.GetPriceMultiplierFor(Poi, GetItemCategory(gi.Item));
            if (m > 1.01f)
            {
                int pct = (int)Math.Round((m - 1.0f) * 100);
                overlay += $" (+{pct}%)";
                color = new Color(0.9f, 0.3f, 0.3f);
            }
            else if (m < 0.99f)
            {
                int pct = (int)Math.Round((1.0f - m) * 100);
                overlay += $" (-{pct}%)";
                color = new Color(0.9f, 0.8f, 0.3f);
            }
        }
        
        var w = ItemGridWidget.Create(gi.Item, CellSize, CellGap, quantity, overlay, color);
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
            int sellPrice = GetSellPrice(source.Item);
            Economy.AddGold(sellPrice);
            OnGoldChanged?.Invoke(Economy.Gold);
        }
        _stock.Add(source.Item);
        return true;
    }

    public void RemoveFromSource(DragSource source)
    {
        // 商店物品被拖到背包：从货架移除 + 扣钱
        if (Economy != null)
        {
            int price = GetBuyPrice(source.Item);
            if (!Economy.SpendGold(price))
            {
                GD.PrintErr($"[ShopGridView] 购买失败：金币不足，item={source.Item.ItemName}, price={price}, gold={Economy.Gold}");
                return;
            }
            OnGoldChanged?.Invoke(Economy.Gold);
        }

        if (_stock != null && _stock.Contains(source.Item))
            _stock.Remove(source.Item);
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
