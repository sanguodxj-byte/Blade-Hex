// GridInventory.cs
// 暗黑破坏神2风格网格背包系统
// - 物品占用不同大小的格子（InvWidth × InvHeight）
// - 可用背包容量受队内角色体质(CON)和力量(STR)影响，力量影响比例最大
// - 支持拖拽放置、自动寻位、整理
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic.Economy;

namespace BladeHex.Strategic;

/// <summary>
/// 背包中已放置的物品条目
/// </summary>
public class GridItem
{
    /// <summary>物品数据引用</summary>
    public ItemData Item { get; set; } = null!;

    /// <summary>在网格中的左上角X坐标</summary>
    public int GridX { get; set; }

    /// <summary>在网格中的左上角Y坐标</summary>
    public int GridY { get; set; }

    /// <summary>堆叠数量（消耗品等可堆叠物品）</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>唯一实例ID（用于区分同类物品的不同实例）</summary>
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N")[..8];

    public int Width => Item.InvWidth;
    public int Height => Item.InvHeight;

    /// <summary>检查此物品是否占据指定格子</summary>
    public bool OccupiesCell(int x, int y)
    {
        return x >= GridX && x < GridX + Width
            && y >= GridY && y < GridY + Height;
    }
}

/// <summary>
/// 暗黑2风格网格背包
/// </summary>
[GlobalClass]
public partial class GridInventory : Resource
{
    // ========================================
    // 容量常量
    // ========================================

    /// <summary>基础网格宽度</summary>
    public const int BaseGridWidth = 10;

    /// <summary>最小网格高度（无队员时）</summary>
    public const int MinGridHeight = 4;

    /// <summary>最大网格高度上限（背包无明显限制）</summary>
    public const int MaxGridHeight = 30;

    /// <summary>力量对背包行数的贡献权重（每点STR贡献）</summary>
    private const float StrWeightPerRow = 0.06f;

    /// <summary>体质对背包行数的贡献权重（每点CON贡献）</summary>
    private const float ConWeightPerRow = 0.03f;

    /// <summary>每个队员的基础行数贡献</summary>
    private const float BaseRowsPerMember = 0.5f;

    // ========================================
    // 状态
    // ========================================

    /// <summary>当前网格宽度</summary>
    public int GridWidth { get; private set; } = BaseGridWidth;

    /// <summary>当前网格高度（受队伍属性影响）</summary>
    public int GridHeight { get; private set; } = MinGridHeight;

    /// <summary>总可用格数</summary>
    public int TotalCells => GridWidth * GridHeight;

    /// <summary>直接设置网格尺寸（商店/战利品布局用）</summary>
    public void SetGridSize(int width, int height)
    {
        GridWidth = Math.Max(1, width);
        GridHeight = Math.Max(1, height);
        _grid = new bool[GridWidth, GridHeight];
    }

    /// <summary>已放置的物品列表</summary>
    public List<GridItem> Items { get; private set; } = new();

    /// <summary>占用网格（true=已占用）</summary>
    private bool[,] _grid = new bool[BaseGridWidth, MinGridHeight];

    // ========================================
    // 容量计算
    // ========================================

    /// <summary>
    /// 根据队伍成员属性重新计算背包容量（已移除限制：固定使用最大高度）
    /// </summary>
    /// <param name="partyMembers">队伍中所有角色</param>
    public void RecalculateCapacity(IEnumerable<UnitData> partyMembers)
    {
        // 取消队伍属性影响：直接使用最大高度，无限制背包
        GridHeight = MaxGridHeight;
        RebuildGrid();
    }

    /// <summary>获取当前已使用的格数</summary>
    public int UsedCells
    {
        get
        {
            int count = 0;
            for (int x = 0; x < GridWidth; x++)
                for (int y = 0; y < GridHeight; y++)
                    if (_grid[x, y]) count++;
            return count;
        }
    }

    /// <summary>获取剩余可用格数</summary>
    public int FreeCells => TotalCells - UsedCells;

    // ========================================
    // 放置与移除
    // ========================================

    /// <summary>
    /// 尝试在指定位置放置物品
    /// </summary>
    /// <returns>是否放置成功</returns>
    public bool TryPlace(ItemData item, int x, int y, int quantity = 1)
    {
        if (TryStackAt(item, x, y, quantity))
            return true;

        if (TryStackAnywhere(item, quantity))
            return true;

        if (!CanPlace(item, x, y)) return false;

        var gridItem = new GridItem
        {
            Item = item,
            GridX = x,
            GridY = y,
            Quantity = quantity,
        };

        Items.Add(gridItem);
        MarkCells(x, y, item.InvWidth, item.InvHeight, true);
        return true;
    }

    /// <summary>
    /// 尝试自动放置物品（找到第一个可用位置）
    /// </summary>
    /// <returns>是否放置成功</returns>
    public bool TryAutoPlace(ItemData item, int quantity = 1)
    {
        if (TryStackAnywhere(item, quantity))
            return true;

        // 从左上角开始逐行扫描寻找空位
        var pos = FindFirstFit(item.InvWidth, item.InvHeight);
        if (pos == null) return false;

        return TryPlace(item, pos.Value.X, pos.Value.Y, quantity);
    }

    /// <summary>
    /// 检查物品是否可以放置在指定位置
    /// </summary>
    public bool CanPlace(ItemData item, int x, int y)
    {
        return CanPlaceSize(item.InvWidth, item.InvHeight, x, y);
    }

    /// <summary>
    /// 检查指定尺寸是否可以放置在指定位置（忽略特定物品）
    /// </summary>
    public bool CanPlaceSize(int width, int height, int x, int y, GridItem? ignore = null)
    {
        // 边界检查
        if (x < 0 || y < 0 || x + width > GridWidth || y + height > GridHeight)
            return false;

        // 碰撞检查
        for (int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                if (_grid[x + dx, y + dy])
                {
                    // 如果有忽略项，检查是否是被忽略的物品占据
                    if (ignore != null && ignore.OccupiesCell(x + dx, y + dy))
                        continue;
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// 移动物品到新位置
    /// </summary>
    /// <returns>是否移动成功</returns>
    public bool TryMove(GridItem gridItem, int newX, int newY)
    {
        if (!Items.Contains(gridItem)) return false;

        // 先临时移除占用
        MarkCells(gridItem.GridX, gridItem.GridY, gridItem.Width, gridItem.Height, false);

        // 检查新位置是否可用
        if (!CanPlaceSize(gridItem.Width, gridItem.Height, newX, newY))
        {
            // 恢复原位
            MarkCells(gridItem.GridX, gridItem.GridY, gridItem.Width, gridItem.Height, true);
            return false;
        }

        // 放置到新位置
        gridItem.GridX = newX;
        gridItem.GridY = newY;
        MarkCells(newX, newY, gridItem.Width, gridItem.Height, true);
        return true;
    }

    /// <summary>
    /// 尝试把 dragged 堆叠到 target 上。成功后 dragged 会从背包中移除。
    /// </summary>
    public bool TryMerge(GridItem dragged, GridItem target)
    {
        if (dragged == target) return false;
        if (!Items.Contains(dragged) || !Items.Contains(target)) return false;
        if (!CanStack(dragged.Item, target.Item)) return false;

        target.Quantity += dragged.Quantity;
        return Remove(dragged);
    }

    /// <summary>尝试把指定数量堆叠到目标格已有同类物品上。</summary>
    public bool TryStackAt(ItemData item, int x, int y, int quantity = 1)
    {
        var target = GetItemAt(x, y);
        if (target == null || !CanStack(item, target.Item)) return false;

        target.Quantity += Math.Max(1, quantity);
        return true;
    }

    /// <summary>尝试把指定数量堆叠到任意已有同类物品上。</summary>
    public bool TryStackAnywhere(ItemData item, int quantity = 1)
    {
        if (!IsStackable(item)) return false;

        var existing = Items.FirstOrDefault(i => CanStack(item, i.Item));
        if (existing == null) return false;

        existing.Quantity += Math.Max(1, quantity);
        return true;
    }

    public bool CanStack(ItemData incoming, ItemData existing)
    {
        return IsStackable(incoming) && IsStackable(existing)
            && incoming.ItemId == existing.ItemId
            && incoming.ItemName == existing.ItemName;
    }

    /// <summary>
    /// 交换两个物品的位置（当拖拽到已有物品上时）
    /// </summary>
    /// <returns>是否交换成功</returns>
    public bool TrySwap(GridItem dragged, GridItem target)
    {
        if (!Items.Contains(dragged) || !Items.Contains(target)) return false;

        int oldX = dragged.GridX, oldY = dragged.GridY;
        int targetX = target.GridX, targetY = target.GridY;

        // 临时移除两者
        MarkCells(dragged.GridX, dragged.GridY, dragged.Width, dragged.Height, false);
        MarkCells(target.GridX, target.GridY, target.Width, target.Height, false);

        // 检查交换后是否都能放下
        bool draggedFits = CanPlaceSize(dragged.Width, dragged.Height, targetX, targetY);
        bool targetFits = draggedFits && CanPlaceSize(target.Width, target.Height, oldX, oldY);

        if (draggedFits && targetFits)
        {
            dragged.GridX = targetX;
            dragged.GridY = targetY;
            target.GridX = oldX;
            target.GridY = oldY;
            MarkCells(dragged.GridX, dragged.GridY, dragged.Width, dragged.Height, true);
            MarkCells(target.GridX, target.GridY, target.Width, target.Height, true);
            return true;
        }

        // 恢复原位
        MarkCells(oldX, oldY, dragged.Width, dragged.Height, true);
        MarkCells(targetX, targetY, target.Width, target.Height, true);
        return false;
    }

    /// <summary>
    /// 从背包中移除物品
    /// </summary>
    public bool Remove(GridItem gridItem)
    {
        if (!Items.Contains(gridItem)) return false;

        MarkCells(gridItem.GridX, gridItem.GridY, gridItem.Width, gridItem.Height, false);
        Items.Remove(gridItem);
        return true;
    }

    /// <summary>
    /// 移除指定数量（堆叠物品），数量归零则完全移除
    /// </summary>
    public bool RemoveQuantity(GridItem gridItem, int quantity = 1)
    {
        if (!Items.Contains(gridItem)) return false;

        gridItem.Quantity -= quantity;
        if (gridItem.Quantity <= 0)
            return Remove(gridItem);
        return true;
    }

    /// <summary>
    /// 按 itemId 查询背包持有数量(跨多个堆叠求和)。
    /// </summary>
    public int GetItemCount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;
        return Items.Where(i => i.Item != null && i.Item.ItemId == itemId).Sum(i => i.Quantity);
    }

    /// <summary>
    /// 按 itemId 移除指定数量,跨多个堆叠;数量不足时全部清光并返回 false。
    /// </summary>
    public bool TryRemove(string itemId, int quantity)
    {
        if (string.IsNullOrEmpty(itemId) || quantity <= 0) return false;

        int remaining = quantity;
        // 拷贝列表避免迭代时修改
        var matching = Items.Where(i => i.Item != null && i.Item.ItemId == itemId).ToList();
        foreach (var gi in matching)
        {
            if (remaining <= 0) break;
            int take = System.Math.Min(remaining, gi.Quantity);
            RemoveQuantity(gi, take);
            remaining -= take;
        }
        return remaining == 0;
    }

    /// <summary>
    /// 获取指定格子上的物品
    /// </summary>
    public GridItem? GetItemAt(int x, int y)
    {
        return Items.FirstOrDefault(item => item.OccupiesCell(x, y));
    }

    // ========================================
    // 自动整理
    // ========================================

    /// <summary>
    /// 自动整理背包：按物品大小降序重新排列，最大化空间利用
    /// </summary>
    public void AutoSort()
    {
        // 按面积降序、然后按宽度降序排列
        var sortedItems = Items
            .OrderByDescending(i => i.Width * i.Height)
            .ThenByDescending(i => i.Width)
            .ThenBy(i => i.Item.ItemName)
            .ToList();

        // 清空网格
        ClearGrid();
        Items.Clear();

        // 重新放置
        foreach (var item in sortedItems)
        {
            var pos = FindFirstFit(item.Width, item.Height);
            if (pos != null)
            {
                item.GridX = pos.Value.X;
                item.GridY = pos.Value.Y;
                Items.Add(item);
                MarkCells(item.GridX, item.GridY, item.Width, item.Height, true);
            }
            else
            {
                // 放不下了（理论上不应该发生，除非容量缩小了）
                GD.PrintErr($"[GridInventory] 整理时无法放置: {item.Item.ItemName}");
            }
        }
    }

    // ========================================
    // 查询
    // ========================================

    /// <summary>按物品名查找</summary>
    public GridItem? FindByName(string itemName)
    {
        return Items.FirstOrDefault(i => i.Item.ItemName == itemName);
    }

    /// <summary>按物品ID查找</summary>
    public GridItem? FindById(string itemId)
    {
        return Items.FirstOrDefault(i => i.Item.ItemId == itemId);
    }

    /// <summary>按实例ID查找</summary>
    public GridItem? FindByInstanceId(string instanceId)
    {
        return Items.FirstOrDefault(i => i.InstanceId == instanceId);
    }

    /// <summary>是否拥有指定物品</summary>
    public bool Has(string itemName, int quantity = 1)
    {
        var item = FindByName(itemName);
        return item != null && item.Quantity >= quantity;
    }

    /// <summary>获取所有物品的总价值</summary>
    public int TotalValue => Items.Sum(i => TradePricingService.GetBasePrice(i.Item) * i.Quantity);

    /// <summary>获取物品总数</summary>
    public int TotalItemCount => Items.Sum(i => i.Quantity);

    // ========================================
    // 内部工具
    // ========================================

    private bool IsStackable(ItemData item)
    {
        // 消耗品和材料可堆叠
        return item is ConsumableData || item.SourceTags.Contains("material") || item.SourceTags.Contains("supply");
    }

    private Vector2I? FindFirstFit(int width, int height)
    {
        for (int y = 0; y < GridHeight; y++)
        {
            for (int x = 0; x < GridWidth; x++)
            {
                if (CanPlaceSize(width, height, x, y))
                    return new Vector2I(x, y);
            }
        }
        return null;
    }

    private void MarkCells(int x, int y, int width, int height, bool occupied)
    {
        for (int dx = 0; dx < width; dx++)
            for (int dy = 0; dy < height; dy++)
                _grid[x + dx, y + dy] = occupied;
    }

    private void ClearGrid()
    {
        for (int x = 0; x < GridWidth; x++)
            for (int y = 0; y < GridHeight; y++)
                _grid[x, y] = false;
    }

    private void RebuildGrid()
    {
        var oldGrid = _grid;
        _grid = new bool[GridWidth, GridHeight];

        // 重新标记所有物品占用
        var itemsToKeep = new List<GridItem>();
        foreach (var item in Items)
        {
            // 检查物品是否仍在有效范围内
            if (item.GridX + item.Width <= GridWidth && item.GridY + item.Height <= GridHeight)
            {
                MarkCells(item.GridX, item.GridY, item.Width, item.Height, true);
                itemsToKeep.Add(item);
            }
            else
            {
                // 尝试重新放置超出范围的物品
                var pos = FindFirstFit(item.Width, item.Height);
                if (pos != null)
                {
                    item.GridX = pos.Value.X;
                    item.GridY = pos.Value.Y;
                    MarkCells(item.GridX, item.GridY, item.Width, item.Height, true);
                    itemsToKeep.Add(item);
                }
                else
                {
                    GD.PrintErr($"[GridInventory] 容量缩小，物品溢出: {item.Item.ItemName}");
                    // 溢出物品暂存（可由上层处理）
                    itemsToKeep.Add(item); // 仍保留引用，UI层可提示玩家
                }
            }
        }
        Items = itemsToKeep;
    }

    // ========================================
    // 序列化
    // ========================================

    public Godot.Collections.Dictionary Serialize()
    {
        var itemsArr = new Godot.Collections.Array();
        foreach (var item in Items)
        {
            itemsArr.Add(new Godot.Collections.Dictionary
            {
                ["item_id"] = item.Item.ItemId,
                ["item_name"] = item.Item.ItemName,
                ["grid_x"] = item.GridX,
                ["grid_y"] = item.GridY,
                ["quantity"] = item.Quantity,
                ["instance_id"] = item.InstanceId,
                ["inv_width"] = item.Width,
                ["inv_height"] = item.Height,
            });
        }

        return new Godot.Collections.Dictionary
        {
            ["grid_width"] = GridWidth,
            ["grid_height"] = GridHeight,
            ["items"] = itemsArr,
        };
    }

    public void Deserialize(Godot.Collections.Dictionary data, Func<string, ItemData?> itemResolver)
    {
        GridWidth = data.ContainsKey("grid_width") ? data["grid_width"].AsInt32() : BaseGridWidth;
        GridHeight = data.ContainsKey("grid_height") ? data["grid_height"].AsInt32() : MinGridHeight;
        _grid = new bool[GridWidth, GridHeight];
        Items.Clear();

        if (!data.ContainsKey("items") || data["items"].Obj is not Godot.Collections.Array itemsArr)
            return;

        foreach (var itemVar in itemsArr)
        {
            if (itemVar.Obj is not Godot.Collections.Dictionary id) continue;

            string itemId = id.ContainsKey("item_id") ? id["item_id"].AsString() : "";
            string itemName = id.ContainsKey("item_name") ? id["item_name"].AsString() : "";
            int gx = id.ContainsKey("grid_x") ? id["grid_x"].AsInt32() : 0;
            int gy = id.ContainsKey("grid_y") ? id["grid_y"].AsInt32() : 0;
            int qty = id.ContainsKey("quantity") ? id["quantity"].AsInt32() : 1;
            string instId = id.ContainsKey("instance_id") ? id["instance_id"].AsString() : "";

            var resolved = itemResolver(itemId);
            if (resolved == null)
            {
                GD.PrintErr($"[GridInventory] 反序列化时无法解析物品: {itemId} ({itemName})");
                continue;
            }

            var gridItem = new GridItem
            {
                Item = resolved,
                GridX = gx,
                GridY = gy,
                Quantity = qty,
                InstanceId = string.IsNullOrEmpty(instId) ? Guid.NewGuid().ToString("N")[..8] : instId,
            };

            Items.Add(gridItem);
            MarkCells(gx, gy, resolved.InvWidth, resolved.InvHeight, true);
        }
    }
}
