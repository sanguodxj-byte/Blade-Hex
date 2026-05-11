// EconomyManager.cs
// 全局经济与库存单例 — 管理金币、食物、时间系统、玩家背包
// 对应策划案 13-经济系统.md
// 迁移自 GDScript EconomyManager.gd
using Godot;
using System.Collections.Generic;

namespace BladeHex.Data;

/// <summary>
/// 全局经济与库存管理器 — Autoload 单例
/// </summary>
[GlobalClass]
public partial class EconomyManager : Node
{
    // ========================================
    // 信号
    // ========================================

    [Signal] public delegate void ResourcesChangedEventHandler();
    [Signal] public delegate void InventoryChangedEventHandler();

    // ========================================
    // 季节枚举
    // ========================================

    public enum Season { Spring, Summer, Fall, Winter }

    // ========================================
    // 核心资源与时间
    // ========================================

    [Export] public int Gold = 1000;
    [Export] public float Food = 20.0f;
    [Export] public float MaxFood = 40.0f;
    [Export] public int DailyWage = 50;

    // 时间系统 (小时制)
    [Export] public float CurrentHour = 8.0f;
    [Export] public int DaysPassed = 1;
    [Export] public int Month = 1;
    [Export] public int Year = 1250;

    // ========================================
    // 玩家背包
    // ========================================

    public Godot.Collections.Array<ItemData> PlayerInventory = new();

    // ========================================
    // 季节查询
    // ========================================

    public Season GetSeason() => Month switch
    {
        <= 3 => Season.Spring,
        <= 6 => Season.Summer,
        <= 9 => Season.Fall,
        _ => Season.Winter,
    };

    public string GetSeasonName() => GetSeason() switch
    {
        Season.Spring => "春季",
        Season.Summer => "夏季",
        Season.Fall => "秋季",
        Season.Winter => "冬季",
        _ => "未知",
    };

    // ========================================
    // 时间推进
    // ========================================

    public void AdvanceTime(float hours)
    {
        CurrentHour += hours;
        while (CurrentHour >= 24.0f)
        {
            CurrentHour -= 24.0f;
            OnDayPassed();
        }
    }

    public void AdvanceDay() => OnDayPassed();

    private void OnDayPassed()
    {
        DaysPassed += 1;
        SpendGold(DailyWage);
        if (DaysPassed > 30)
        {
            DaysPassed = 1;
            Month += 1;
            if (Month > 12)
            {
                Month = 1;
                Year += 1;
            }
        }
        EmitSignal(SignalName.ResourcesChanged);
    }

    // ========================================
    // 金币操作
    // ========================================

    public void AddGold(int amount)
    {
        Gold += amount;
        EmitSignal(SignalName.ResourcesChanged);
    }

    public bool SpendGold(int amount)
    {
        if (Gold >= amount)
        {
            Gold -= amount;
            EmitSignal(SignalName.ResourcesChanged);
            return true;
        }
        return false;
    }

    // ========================================
    // 食物操作
    // ========================================

    public void ConsumeFood(float amount)
    {
        Food = Mathf.Max(0.0f, Food - amount);
        EmitSignal(SignalName.ResourcesChanged);
    }

    public void AddFood(float amount)
    {
        Food = Mathf.Min(MaxFood, Food + amount);
        EmitSignal(SignalName.ResourcesChanged);
    }

    // ========================================
    // 背包操作
    // ========================================

    public void AddItem(ItemData item)
    {
        PlayerInventory.Add(item);
        EmitSignal(SignalName.InventoryChanged);
    }

    public void RemoveItem(ItemData item)
    {
        int idx = PlayerInventory.IndexOf(item);
        if (idx >= 0)
        {
            PlayerInventory.RemoveAt(idx);
            EmitSignal(SignalName.InventoryChanged);
        }
    }

    public bool HasItem(string itemId)
    {
        foreach (var item in PlayerInventory)
            if (item.ItemId == itemId) return true;
        return false;
    }

    public ItemData? FindItem(string itemId)
    {
        foreach (var item in PlayerInventory)
            if (item.ItemId == itemId) return item;
        return null;
    }

    /// <summary>查找背包中所有指定类型的物品</summary>
    public T[] FindItemsOfType<T>() where T : ItemData
    {
        var result = new List<T>();
        foreach (var item in PlayerInventory)
            if (item is T typed) result.Add(typed);
        return result.ToArray();
    }
}
