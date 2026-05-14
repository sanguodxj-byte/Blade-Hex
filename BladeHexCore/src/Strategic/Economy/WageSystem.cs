// WageSystem.cs
// 工资系统 — 每日结算队伍工资，欠饷导致士气下降和离队
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 工资系统 — 骑砍核心经济压力来源
/// </summary>
public class WageSystem
{
    /// <summary>每人每天基础工资（按等级缩放）</summary>
    public float BaseDailyWagePerLevel = 2.0f;

    /// <summary>连续欠饷天数</summary>
    public int ConsecutiveUnpaidDays { get; private set; } = 0;

    /// <summary>上次结算日</summary>
    public int LastPayDay { get; set; } = 0;

    /// <summary>
    /// 每日结算工资
    /// </summary>
    /// <returns>结算结果</returns>
    public WageResult ProcessDaily(PartyRoster roster, int currentDay, Func<int, bool> trySpendGold)
    {
        var result = new WageResult { Day = currentDay };

        if (roster == null || roster.Count <= 1) return result; // 只有队长不扣

        // 计算总工资（队长不扣）
        int totalWage = 0;
        foreach (var member in roster.Members)
        {
            if (roster.IsLeader(member)) continue;
            totalWage += GetDailyWage(member);
        }

        result.TotalWageDue = totalWage;

        // 尝试扣钱
        if (totalWage > 0 && trySpendGold(totalWage))
        {
            result.Paid = true;
            ConsecutiveUnpaidDays = 0;
        }
        else
        {
            result.Paid = false;
            ConsecutiveUnpaidDays++;
            result.UnpaidDays = ConsecutiveUnpaidDays;

            // 欠饷惩罚
            ApplyUnpaidPenalty(roster, result);
        }

        LastPayDay = currentDay;
        return result;
    }

    /// <summary>计算单个队员每日工资</summary>
    public int GetDailyWage(UnitData unit)
    {
        return Math.Max(1, (int)(BaseDailyWagePerLevel * unit.Level));
    }

    /// <summary>计算队伍每日总工资</summary>
    public int GetTotalDailyWage(PartyRoster roster)
    {
        int total = 0;
        foreach (var m in roster.Members)
        {
            if (roster.IsLeader(m)) continue;
            total += GetDailyWage(m);
        }
        return total;
    }

    /// <summary>预测能撑几天</summary>
    public int PredictDaysUntilBroke(PartyRoster roster, int currentGold)
    {
        int daily = GetTotalDailyWage(roster);
        if (daily <= 0) return 999;
        return currentGold / daily;
    }

    private void ApplyUnpaidPenalty(PartyRoster roster, WageResult result)
    {
        // 每天欠饷：全员士气 -10
        foreach (var m in roster.Members)
        {
            if (roster.IsLeader(m)) continue;
            m.Morale = Math.Max(-100, m.Morale - 10);
        }

        // 连续 3 天：最低士气的队员离队
        if (ConsecutiveUnpaidDays >= 3 && ConsecutiveUnpaidDays % 3 == 0)
        {
            UnitData? worstMorale = null;
            int worstVal = int.MaxValue;
            foreach (var m in roster.Members)
            {
                if (roster.IsLeader(m)) continue;
                if (m.Morale < worstVal) { worstVal = m.Morale; worstMorale = m; }
            }
            if (worstMorale != null)
            {
                roster.Remove(worstMorale);
                result.DesertedUnits.Add(worstMorale.UnitName);
                GD.Print($"[WageSystem] {worstMorale.UnitName} 因欠饷离队！(士气 {worstVal})");
            }
        }

        // 连续 7 天：50% 队员集体离队
        if (ConsecutiveUnpaidDays == 7)
        {
            var toDesert = new List<UnitData>();
            var rng = new Random();
            foreach (var m in roster.Members)
            {
                if (roster.IsLeader(m)) continue;
                if (rng.NextDouble() < 0.5) toDesert.Add(m);
            }
            foreach (var m in toDesert)
            {
                roster.Remove(m);
                result.DesertedUnits.Add(m.UnitName);
            }
            if (toDesert.Count > 0)
                GD.Print($"[WageSystem] 集体离队！{toDesert.Count} 人因长期欠饷离开");
        }
    }

    /// <summary>序列化</summary>
    public Godot.Collections.Dictionary Serialize() => new()
    {
        ["unpaid_days"] = ConsecutiveUnpaidDays,
        ["last_pay_day"] = LastPayDay,
    };

    /// <summary>反序列化</summary>
    public static WageSystem Deserialize(Godot.Collections.Dictionary data)
    {
        var ws = new WageSystem();
        ws.ConsecutiveUnpaidDays = data.ContainsKey("unpaid_days") ? data["unpaid_days"].AsInt32() : 0;
        ws.LastPayDay = data.ContainsKey("last_pay_day") ? data["last_pay_day"].AsInt32() : 0;
        return ws;
    }
}

/// <summary>工资结算结果</summary>
public class WageResult
{
    public int Day;
    public int TotalWageDue;
    public bool Paid;
    public int UnpaidDays;
    public List<string> DesertedUnits = new();
}
