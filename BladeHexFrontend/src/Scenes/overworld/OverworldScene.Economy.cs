// OverworldScene.Economy.cs
// 大地图经济系统接线 — 每日工资/食物结算
using Godot;
using BladeHex.Strategic;
using BladeHex.Data;
using BladeHex.Events;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene
{
    private WageSystem _wageSystem = new();
    private FoodSystem _foodSystem = new();
    private ReputationTracker _reputationTracker = new();
    private int _lastProcessedDay = 0;

    /// <summary>初始化经济子系统（在 InitEntityManager 之后调用）</summary>
    private void InitEconomySystems()
    {
        // 订阅每日事件
        EventBus.Instance?.Subscribe(EventBus.Signals.DayPassed, OnDayPassedEconomy);
    }

    private void OnDayPassedEconomy(Godot.Collections.Dictionary data)
    {
        int day = data.ContainsKey("day") ? data["day"].AsInt32() : 1;
        if (day == _lastProcessedDay) return; // 防重复
        _lastProcessedDay = day;

        if (PlayerParty?.Roster == null || EconomyMgr == null) return;

        // 1. 工资结算
        var wageResult = _wageSystem.ProcessDaily(
            PlayerParty.Roster, day,
            amount => EconomyMgr.SpendGold(amount));

        if (!wageResult.Paid && wageResult.UnpaidDays > 0)
            GD.Print($"[Economy] 欠饷第 {wageResult.UnpaidDays} 天！");
        if (wageResult.DesertedUnits.Count > 0)
            GD.Print($"[Economy] 离队: {string.Join(", ", wageResult.DesertedUnits)}");

        // 2. 食物结算
        float food = EconomyMgr.Food;
        var foodResult = _foodSystem.ProcessDaily(PlayerParty.Roster, ref food);
        EconomyMgr.Food = food;

        if (foodResult.Starving)
            GD.Print($"[Economy] 断粮第 {foodResult.StarveDays} 天！");

        // 3. HP 恢复（非断粮时每天恢复 2 HP）
        if (_foodSystem.CanRestoreHp)
            PlayerParty.Roster.RestoreHp(2);

        // 4. 检查升级
        CampSystem.CheckAndApplyLevelUps(PlayerParty.Roster);
    }
}
