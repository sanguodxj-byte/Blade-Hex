// EconomyManager.cs
// 全局经济与库存单例 — 管理金币、食物、时间系统、玩家背包
// 对应策划案 13-经济系统.md
using Godot;
using System.Collections.Generic;
using BladeHex.Events;
using BladeHex.Strategic;

namespace BladeHex.Data;

/// <summary>
/// 全局经济与库存管理器 — Autoload 单例
/// 实现 ITimeProvider，将时间源注册到 Core 层
/// </summary>
[GlobalClass]
public partial class EconomyManager : Node, ITimeProvider
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

    [Export] public int Gold { get; set; } = 1000;
    [Export] public float Food { get; set; } = 20.0f;
    [Export] public float MaxFood { get; set; } = 40.0f;
    [Export] public int DailyWage { get; set; } = 50;

    // 时间系统 (小时制)
    [Export] public float CurrentHour { get; set; } = 8.0f;
    [Export] public int DaysPassed { get; set; } = 1;
    [Export] public int Month { get; set; } = 1;
    [Export] public int Year { get; set; } = 1250;

    // ITimeProvider 实现 — 将时间源暴露给 Core 层
    int ITimeProvider.CurrentDay => DaysPassed;

    // ========================================
    // 玩家背包
    // ========================================

    public Godot.Collections.Array<ItemData> PlayerInventory = new();

    // ========================================
    // 生命周期 — 注册时间提供者
    // ========================================

    public override void _EnterTree()
    {
        TimeProvider.Set(this);
    }

    public override void _ExitTree()
    {
        TimeProvider.Clear();
    }

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
        EventBus.Instance?.Publish(EventBus.Signals.DayPassed, new Godot.Collections.Dictionary
        {
            { "day", DaysPassed }, { "month", Month }, { "year", Year },
            { "season", GetSeasonName() }, { "gold", Gold }, { "food", Food },
        });
        EmitSignal(SignalName.ResourcesChanged);
    }

    // ========================================
    // 金币操作
    // ========================================

    public void AddGold(int amount)
    {
        int old = Gold;
        Gold += amount;
        EventBus.Instance?.PublishGoldChanged(old, Gold, amount);
        EmitSignal(SignalName.ResourcesChanged);
    }

    public bool SpendGold(int amount)
    {
        if (Gold >= amount)
        {
            int old = Gold;
            Gold -= amount;
            EventBus.Instance?.PublishGoldChanged(old, Gold, -amount);
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
        float old = Food;
        Food = Mathf.Max(0.0f, Food - amount);
        EventBus.Instance?.Publish(EventBus.Signals.FoodChanged, new Godot.Collections.Dictionary
        {
            { "old_amount", old }, { "new_amount", Food }, { "delta", -amount },
        });
        EmitSignal(SignalName.ResourcesChanged);
    }

    public void AddFood(float amount)
    {
        float old = Food;
        Food = Mathf.Min(MaxFood, Food + amount);
        EventBus.Instance?.Publish(EventBus.Signals.FoodChanged, new Godot.Collections.Dictionary
        {
            { "old_amount", old }, { "new_amount", Food }, { "delta", amount },
        });
        EmitSignal(SignalName.ResourcesChanged);
    }

    // ========================================
    // 背包操作
    // ========================================

    public void AddItem(ItemData item)
    {
        PlayerInventory.Add(item);
        EventBus.Instance?.Publish(EventBus.Signals.ItemAcquired, new Godot.Collections.Dictionary
        {
            { "item_id", item.ItemId }, { "item_name", item.ItemName },
        });
        EmitSignal(SignalName.InventoryChanged);
    }

    public void RemoveItem(ItemData item)
    {
        int idx = PlayerInventory.IndexOf(item);
        if (idx >= 0)
        {
            PlayerInventory.RemoveAt(idx);
            EventBus.Instance?.Publish(EventBus.Signals.ItemLost, new Godot.Collections.Dictionary
            {
                { "item_id", item.ItemId }, { "item_name", item.ItemName },
            });
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
