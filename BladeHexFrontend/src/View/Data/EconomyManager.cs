// EconomyManager.cs
// 全局经济与库存单例 — 管理金币、食物、时间系统、玩家背包
// 以及四大生存资源（金币/食物/工具/药品）的每日结算
// 对应策划案 13-经济系统.md
using Godot;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Events;
using BladeHex.Strategic;
using BladeHex.Strategic.Economy;
using BladeHex.Strategic.Facilities;

namespace BladeHex.Data;

/// <summary>
/// 全局经济与库存管理器 — Autoload 单例
/// 实现 ITimeProvider 和 IEconomyProvider，将时间和经济操作注册到 Core 层
/// </summary>
[GlobalClass]
public partial class EconomyManager : Node, ITimeProvider, IEconomyProvider
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

    // 食物
    [Export] public float Food { get; set; } = 20.0f;
    [Export] public float MaxFood { get; set; } = 40.0f;

    // 工具（装备保养 / 船只修复）
    [Export] public float Tools { get; set; } = 20.0f;
    [Export] public float MaxTools { get; set; } = 40.0f;

    // 药品（伤员治疗 / 重伤手术）
    [Export] public float Medicine { get; set; } = 20.0f;
    [Export] public float MaxMedicine { get; set; } = 40.0f;

    // 旧字段保留兼容（UI 仍在使用静态 DailyWage）
    [Export] public int DailyWage { get; set; } = 0;

    // 时间系统 (小时制)
    [Export] public float CurrentHour { get; set; } = 8.0f;
    [Export] public int DaysPassed { get; set; } = 1;
    [Export] public int Month { get; set; } = 1;
    [Export] public int Year { get; set; } = 1250;

    // ITimeProvider 实现 — 将时间源暴露给 Core 层
    int ITimeProvider.CurrentDay => DaysPassed;

    // IEconomyProvider 实现 — 将经济操作暴露给 Core 层
    int IEconomyProvider.Gold => Gold;
    int IEconomyProvider.DaysPassed => DaysPassed;
    void IEconomyProvider.AddGold(int amount) => AddGold(amount);
    bool IEconomyProvider.SpendGold(int amount) => SpendGold(amount);

    // ========================================
    // 生存子系统（Core 层）
    // ========================================

    /// <summary>军饷结算子系统</summary>
    public WageSystem WageSys { get; } = new();

    /// <summary>食物消耗子系统</summary>
    public FoodSystem FoodSys { get; } = new();

    // ========================================
    // 队伍名册（由大地图注入，用于动态计算军饷与口粮）
    // ========================================

    /// <summary>
    /// 当前队伍名册 — 由 OverworldScene3D.InitPlayer 在创建完 PlayerParty 后注入。
    /// 未注入时所有动态计算退化为安全默认值（0 成员）。
    /// </summary>
    public PartyRoster? ActiveRoster { get; set; } = null;

    // ========================================
    // 玩家背包
    // ========================================

    private Godot.Collections.Array<ItemData> PlayerInventory = new();

    /// <summary>获取背包物品数量</summary>
    public int PlayerInventoryCount => PlayerInventory.Count;

    /// <summary>枚举背包中所有物品（用于序列化/遍历）</summary>
    public System.Collections.Generic.IEnumerable<ItemData> GetAllItems() => PlayerInventory;

    // ========================================
    // 生命周期 — 注册时间提供者
    // ========================================

    public override void _EnterTree()
    {
        TimeProvider.Set(this);
        EconomyProvider.Set(this);
    }

    public override void _ExitTree()
    {
        TimeProvider.Clear();
        EconomyProvider.Clear();
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

    /// <summary>
    /// 推进时间。每次推进都触发逐小时比例恢复。
    /// </summary>
    /// <param name="hours">推进小时数</param>
    /// <param name="recoveryRate">恢复倍率（1=正常，2=扎营，4=城内旅店）</param>
    public void AdvanceTime(float hours, float recoveryRate = 1.0f)
    {
        CurrentHour += hours;

        // 逐小时比例恢复（在日结之前处理，使用当前 canRestore 状态）
        if (ActiveRoster != null && hours > 0)
        {
            bool canRestore = WageSys.CanRestore && FoodSys.ConsecutiveStarveDays == 0;
            var (hp, mana) = RestService.TimeBasedRecovery(ActiveRoster, hours, canRestore, recoveryRate);
            if (hp > 0 || mana > 0)
                GD.Print($"[EconomyManager] 时间推进 {hours}h: HP+{hp}, 法力+{mana} (x{recoveryRate})");
        }

        while (CurrentHour >= 24.0f)
        {
            CurrentHour -= 24.0f;
            OnDayPassed();
        }
    }

    public void AdvanceDay() => OnDayPassed();

    /// <summary>行军食物每小时消耗 — 由大地图 _Process 每帧调用（动态）</summary>
    public void ConsumeFoodByTravel(float deltaHours)
    {
        if (ActiveRoster == null) return;
        float hourlyConsumption = (ActiveRoster.Count * FoodSys.FoodPerMemberPerDay) / 24.0f;
        float consumed = hourlyConsumption * deltaHours;
        if (consumed <= 0) return;
        float old = Food;
        Food = Mathf.Max(0.0f, Food - consumed);

        // 断粮检测与惩罚（替代已移除的每日双重扣粮）
        if (Food <= 0f && old > 0f)
        {
            FoodSys.IncrementStarveDays();
            ApplyStarvationPenalty();
        }
        else if (Food > 0f)
        {
            FoodSys.ResetStarveDays();
        }

        EventBus.Instance?.Publish(EventBus.Signals.FoodChanged, new Godot.Collections.Dictionary
        {
            { "old_amount", old }, { "new_amount", Food }, { "delta", -consumed },
        });
        EmitSignal(SignalName.ResourcesChanged);
    }

    /// <summary>断粮惩罚：打印警告</summary>
    private void ApplyStarvationPenalty()
    {
        if (ActiveRoster == null) return;
        GD.Print($"[EconomyManager] 断粮！连续 {FoodSys.ConsecutiveStarveDays} 天无口粮。");
    }

    private void OnDayPassed()
    {
        DaysPassed += 1;
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

        // ──────────────────────────────────────
        // 1. 军饷结算（WageSystem）
        // ──────────────────────────────────────
        if (ActiveRoster != null)
        {
            var wageResult = WageSys.ProcessDaily(ActiveRoster, DaysPassed, amt => SpendGold(amt));
            if (!wageResult.Paid && wageResult.TotalWageDue > 0)
            {
                GD.Print($"[EconomyManager] 连续欠饷 {wageResult.UnpaidDays} 天！");
                EventBus.Instance?.Publish(EventBus.Signals.DayPassed, new Godot.Collections.Dictionary
                {
                    { "event_type", "wage_unpaid" },
                    { "unpaid_days", wageResult.UnpaidDays },
                });
            }
            // (欠饷不离队，仅阻止自然恢复 HP/法力)
        }

        // ──────────────────────────────────────
        // 2. 食物：已移除每日双重扣粮
        //    口粮消耗统一由 ConsumeFoodByTravel 按小时结算，
        //    避免行军时"按小时 + 按天"双重扣除。
        //    停留在城镇时不吃行军粮（食宿在设施中解决）。
        // ──────────────────────────────────────

        // ──────────────────────────────────────
        // 3. 工具每日消耗（装备保养）
        // ──────────────────────────────────────
        if (ActiveRoster != null && ActiveRoster.Count > 0)
        {
            float toolsNeeded = ActiveRoster.Count * 0.1f; // 每人每天 0.1 单位工具
            if (Tools >= toolsNeeded)
            {
                Tools -= toolsNeeded;
            }
            else
            {
                Tools = 0;
                GD.Print("[EconomyManager] 工具匮乏！装备开始生锈，战斗中护甲减益风险激增。");
            }
        }

        // ──────────────────────────────────────
        // 4. 药品每日消耗（伤员维护）
        // ──────────────────────────────────────
        if (ActiveRoster != null)
        {
            float medicineNeeded = 0f;
            bool hasWounded = false;
            foreach (var member in ActiveRoster.Members)
            {
                int curHp = PartyRoster.GetCurrentHp(member);
                if (curHp < member.BaseMaxHp)
                    medicineNeeded += 0.2f; // 未满血：每人每天 0.2 单位
                if (member.IsWounded)
                {
                    medicineNeeded += 1.0f; // 重伤：每人每天额外 1.0 单位
                    hasWounded = true;
                }
            }
            if (medicineNeeded > 0)
            {
                if (Medicine >= medicineNeeded)
                {
                    Medicine -= medicineNeeded;
                }
                else
                {
                    Medicine = 0;
                    if (hasWounded)
                    {
                        GD.Print("[EconomyManager] 药品匮乏！重伤成员自愈时间延长。");
                    }
                }
            }
        }

        // ──────────────────────────────────────
        // 5. 发布每日事件
        // ──────────────────────────────────────
        EventBus.Instance?.Publish(EventBus.Signals.DayPassed, new Godot.Collections.Dictionary
        {
            { "day", DaysPassed }, { "month", Month }, { "year", Year },
            { "season", GetSeasonName() }, { "gold", Gold }, { "food", Food },
            { "tools", Tools }, { "medicine", Medicine },
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

    /// <summary>直接消耗食物（不使用动态人数，用于非行军情境）</summary>
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
    // 工具操作
    // ========================================

    public void AddTools(float amount)
    {
        Tools = Mathf.Min(MaxTools, Tools + amount);
        EmitSignal(SignalName.ResourcesChanged);
    }

    public bool ConsumeTools(float amount)
    {
        if (Tools >= amount)
        {
            Tools -= amount;
            EmitSignal(SignalName.ResourcesChanged);
            return true;
        }
        return false;
    }

    // ========================================
    // 药品操作
    // ========================================

    public void AddMedicine(float amount)
    {
        Medicine = Mathf.Min(MaxMedicine, Medicine + amount);
        EmitSignal(SignalName.ResourcesChanged);
    }

    public bool ConsumeMedicine(float amount)
    {
        if (Medicine >= amount)
        {
            Medicine -= amount;
            EmitSignal(SignalName.ResourcesChanged);
            return true;
        }
        return false;
    }

    // ========================================
    // 财务预测查询
    // ========================================

    /// <summary>当前每日军饷总额（基于 ActiveRoster 动态计算）</summary>
    public int GetDailyWageTotal()
    {
        if (ActiveRoster == null) return 0;
        return WageSys.GetTotalDailyWage(ActiveRoster);
    }

    /// <summary>预计金库能支撑的天数</summary>
    public int GetDaysUntilBroke()
    {
        if (ActiveRoster == null) return 999;
        return WageSys.PredictDaysUntilBroke(ActiveRoster, Gold);
    }

    /// <summary>每日食物消耗量（基于 ActiveRoster 动态计算）</summary>
    public float GetDailyFoodConsumption()
    {
        if (ActiveRoster == null) return 0;
        return ActiveRoster.Count * FoodSys.FoodPerMemberPerDay;
    }

    /// <summary>预计口粮能支撑的天数</summary>
    public int GetDaysUntilStarving()
    {
        float daily = GetDailyFoodConsumption();
        if (daily <= 0) return 999;
        return (int)(Food / daily);
    }

    /// <summary>预计工具能支撑的天数</summary>
    public int GetDaysUntilToolsDepleted()
    {
        if (ActiveRoster == null || ActiveRoster.Count == 0) return 999;
        float daily = ActiveRoster.Count * 0.1f;
        if (daily <= 0) return 999;
        return (int)(Tools / daily);
    }

    /// <summary>预计药品能支撑的天数</summary>
    public int GetDaysUntilMedicineDepleted()
    {
        if (ActiveRoster == null || ActiveRoster.Count == 0) return 999;
        float daily = ActiveRoster.Count * 0.2f;
        if (daily <= 0) return 999;
        return (int)(Medicine / daily);
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
