// FoodSystem.cs
// 食物消耗系统 — 按队伍人数每日消耗食物，断粮影响士气和恢复
using Godot;
using System;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 食物系统 — 骑砍式补给压力
/// </summary>
public class FoodSystem
{
    /// <summary>每人每天消耗食物单位</summary>
    public float FoodPerMemberPerDay = 0.5f;

    /// <summary>断粮连续天数</summary>
    public int ConsecutiveStarveDays { get; private set; } = 0;

    /// <summary>
    /// 每日结算食物消耗
    /// </summary>
    /// <returns>结算结果</returns>
    public FoodResult ProcessDaily(PartyRoster roster, ref float currentFood)
    {
        var result = new FoodResult();
        if (roster == null) return result;

        float needed = roster.Count * FoodPerMemberPerDay;
        result.FoodConsumed = needed;
        result.FoodBefore = currentFood;

        if (currentFood >= needed)
        {
            currentFood -= needed;
            ConsecutiveStarveDays = 0;
            result.Starving = false;
        }
        else
        {
            // 有多少吃多少
            currentFood = 0;
            ConsecutiveStarveDays++;
            result.Starving = true;
            result.StarveDays = ConsecutiveStarveDays;

            // 断粮惩罚：每天士气 -5，HP 不恢复（由 RestoreHp 检查）
            foreach (var m in roster.Members)
            {
                m.Morale = Math.Max(-100, m.Morale - 5);
            }

            if (ConsecutiveStarveDays >= 3)
                GD.Print($"[FoodSystem] 断粮 {ConsecutiveStarveDays} 天！全员士气持续下降");
        }

        result.FoodAfter = currentFood;
        result.DaysRemaining = needed > 0 ? (int)(currentFood / needed) : 999;
        return result;
    }

    /// <summary>是否允许 HP 恢复（断粮时不恢复）</summary>
    public bool CanRestoreHp => ConsecutiveStarveDays == 0;

    /// <summary>序列化</summary>
    public Godot.Collections.Dictionary Serialize() => new()
    {
        ["starve_days"] = ConsecutiveStarveDays,
    };

    public static FoodSystem Deserialize(Godot.Collections.Dictionary data)
    {
        var fs = new FoodSystem();
        fs.ConsecutiveStarveDays = data.ContainsKey("starve_days") ? data["starve_days"].AsInt32() : 0;
        return fs;
    }
}

/// <summary>食物结算结果</summary>
public class FoodResult
{
    public float FoodConsumed;
    public float FoodBefore;
    public float FoodAfter;
    public bool Starving;
    public int StarveDays;
    public int DaysRemaining;
}
