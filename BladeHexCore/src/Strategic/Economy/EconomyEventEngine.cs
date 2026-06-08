// EconomyEventEngine.cs
// 价格波动事件管理器
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic.Economy;

public class EconomyEventEngine
{
    public List<EconomyEvent> ActiveEvents { get; set; } = new();

    /// <summary>
    /// 每日 Tick：更新已过期的事件，并根据大地图状态生成最新的战争/围城事件
    /// </summary>
    public void Tick(WorldTickContext ctx, WorldEventEngine worldEngine)
    {
        if (ctx.Pois == null) return;

        // 1. 保留持久性事件（如 CapturedInflation），剔除过期项
        ActiveEvents.RemoveAll(e => ctx.CurrentDay >= e.ExpiresDay);

        // 2. 每天根据实时局势重构短期动态事件（如 War, Siege）
        // 从而避免战争/围城结束后，物价多余的滞后影响。
        ActiveEvents.RemoveAll(e => e.Type == EconomyEventType.War || e.Type == EconomyEventType.Siege);

        foreach (var poi in ctx.Pois)
        {
            // A. 围城事件 (Siege) -> 繁荣度扣减且所有商品物价 x1.4
            if (poi.IsUnderSiege || poi.SiegeDays > 0)
            {
                ActiveEvents.Add(new EconomyEvent(
                    EconomyEventType.Siege,
                    poi.PoiName,
                    poi.OwningFaction,
                    "all",
                    1.4f,
                    ctx.CurrentDay + 1 // 每日 Tick 重建，寿命设为1天
                ));
            }

            // B. 战争事件 (War) -> 参战国物价上涨
            if (!string.IsNullOrEmpty(poi.OwningFaction) && poi.OwningFaction != "neutral" && poi.OwningFaction != "hostile")
            {
                // 检查是否有任何国家和该据点所属国家开战
                if (worldEngine.ActiveWars.Any(w => w.NationA == poi.OwningFaction || w.NationB == poi.OwningFaction))
                {
                    // 武器盔甲 x1.3
                    ActiveEvents.Add(new EconomyEvent(
                        EconomyEventType.War,
                        poi.PoiName,
                        poi.OwningFaction,
                        "weapon",
                        1.3f,
                        ctx.CurrentDay + 1
                    ));
                    // 食物口粮 x1.2
                    ActiveEvents.Add(new EconomyEvent(
                        EconomyEventType.War,
                        poi.PoiName,
                        poi.OwningFaction,
                        "food",
                        1.2f,
                        ctx.CurrentDay + 1
                    ));
                    // 马匹 x1.5
                    ActiveEvents.Add(new EconomyEvent(
                        EconomyEventType.War,
                        poi.PoiName,
                        poi.OwningFaction,
                        "horse",
                        1.5f,
                        ctx.CurrentDay + 1
                    ));
                }
            }
        }
    }

    /// <summary>
    /// 获取特定据点、特定品类商品的最终物价累计乘数
    /// </summary>
    public float GetPriceMultiplierFor(OverworldPOI poi, string category)
    {
        if (poi == null) return 1.0f;

        float totalMultiplier = 1.0f;

        foreach (var e in ActiveEvents)
        {
            // 检查事件作用域 (全境 FactionId 匹配，或特定据点 TargetPoiName 匹配)
            bool scopeMatch = (!string.IsNullOrEmpty(e.TargetPoiName) && e.TargetPoiName == poi.PoiName) ||
                              (!string.IsNullOrEmpty(e.FactionId) && e.FactionId == poi.OwningFaction);

            if (!scopeMatch) continue;

            // 检查品类匹配
            bool categoryMatch = e.ItemCategory == "all" || e.ItemCategory == category;
            if (categoryMatch)
            {
                totalMultiplier *= e.Multiplier;
            }
        }

        return totalMultiplier;
    }

    /// <summary>
    /// 当玩家俘虏对方领主时调用，引发该势力全境 7 天 +5% 的通胀 ( लॉर्ड कैप्चर मुद्रास्फीति )
    /// </summary>
    public void TriggerCapturedInflation(string factionId, int currentDay)
    {
        if (string.IsNullOrEmpty(factionId)) return;

        ActiveEvents.Add(new EconomyEvent(
            EconomyEventType.LordCapturedInflation,
            "",
            factionId,
            "all",
            1.05f,
            currentDay + 7
        ));
    }

    // ========================================
    // 序列化与存档兼容
    // ========================================

    public Godot.Collections.Array Serialize()
    {
        var arr = new Godot.Collections.Array();
        foreach (var e in ActiveEvents)
        {
            arr.Add(new Godot.Collections.Dictionary
            {
                { "type", (int)e.Type },
                { "poi", e.TargetPoiName },
                { "faction", e.FactionId },
                { "category", e.ItemCategory },
                { "mult", e.Multiplier },
                { "expires", e.ExpiresDay }
            });
        }
        return arr;
    }

    public void Deserialize(Godot.Collections.Array data)
    {
        ActiveEvents.Clear();
        if (data == null) return;

        foreach (var item in data)
        {
            var d = (Godot.Collections.Dictionary)item;
            ActiveEvents.Add(new EconomyEvent(
                (EconomyEventType)d["type"].AsInt32(),
                d["poi"].AsString(),
                d["faction"].AsString(),
                d["category"].AsString(),
                d["mult"].AsSingle(),
                d["expires"].AsInt32()
            ));
        }
    }
}
