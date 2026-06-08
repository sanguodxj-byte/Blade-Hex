using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BladeHex.Strategic.WorldEvents;

/// <summary>
/// 战争目标规划器
/// </summary>
public static class WarObjectivePlanner
{
    /// <summary>
    /// 评估并刷新战争双方的攻势目标
    /// </summary>
    public static void RefreshObjectives(WarState war, WorldTickContext ctx)
    {
        if (ctx.Pois == null || ctx.Pois.Count == 0) return;

        // 每 5 天刷新一次，或者如果未初始化（LastObjectiveRefreshDay == 0 且列表为空）则执行首次刷新
        bool shouldRefresh = (ctx.CurrentDay - war.LastObjectiveRefreshDay >= 5) || 
                            (war.LastObjectiveRefreshDay == 0 && war.ObjectivesA.Count == 0 && war.ObjectivesB.Count == 0);

        // 如果到了刷新期，重新选择目标
        if (shouldRefresh)
        {
            war.ObjectivesA = PlanObjectivesForNation(war.NationA, war.NationB, ctx);
            war.ObjectivesB = PlanObjectivesForNation(war.NationB, war.NationA, ctx);
            war.LastObjectiveRefreshDay = ctx.CurrentDay;
        }
        else
        {
            // 剔除不再属于对方的 POI 目标
            CleanInvalidObjectives(war, ctx);
        }
    }

    /// <summary>
    /// 为特定国家规划其针对敌国的攻势目标
    /// </summary>
    private static List<string> PlanObjectivesForNation(string attacker, string defender, WorldTickContext ctx)
    {
        if (ctx.Pois == null) return new List<string>();

        // 筛选攻方拥有的 POI(用于距离评分);若攻方已亡国,放弃攻势计划
        var attackerPois = ctx.Pois.Where(p => p.OwningFaction == attacker).ToList();
        if (attackerPois.Count == 0) return new List<string>();

        // 筛选防守方的 POI（仅选择核心城镇和城堡作为战争目标，排除附属村庄、矿场和农庄）
        var defenderPois = ctx.Pois.Where(p => 
            p.OwningFaction == defender && 
            (p.PoiTypeEnum == OverworldPOI.POIType.Town || p.PoiTypeEnum == OverworldPOI.POIType.Castle)
        ).ToList();
        if (defenderPois.Count == 0) return new List<string>();

        // 1. 计算进攻方 POI 的几何质心 (O(A))
        Vector2 attackerCentroid = Vector2.Zero;
        foreach (var ap in attackerPois)
        {
            attackerCentroid += ap.Position;
        }
        attackerCentroid /= attackerPois.Count;

        var scoredTargets = new List<(OverworldPOI Poi, float Score)>();

        // 2. 遍历防守方 POI 仅与质心求距 (O(D))
        foreach (var poi in defenderPois)
        {
            // 双保险:不应把己方 POI 选为目标
            if (poi.OwningFaction == attacker) continue;

            // 计算该 POI 距离质心的距离
            float distance = attackerCentroid.DistanceTo(poi.Position);

            // 获取防御力
            float defense = poi.GetDefensePower();

            // 评分公式:繁荣度 - 防御力 - (距离 / 10)
            float score = poi.Prosperity - defense - (distance / 10.0f);
            scoredTargets.Add((poi, score));
        }

        // 按评分倒序排序
        var sorted = scoredTargets.OrderByDescending(t => t.Score).ToList();

        var result = new List<string>();
        // 最多选择 5 个目标:前 2 个为 High,后 3 个为 Normal
        // 在查询优先级时,前 2 个元素即为 High 优先级目标,剩余的为 Normal
        int count = Math.Min(5, sorted.Count);
        for (int i = 0; i < count; i++)
        {
            result.Add(sorted[i].Poi.PoiName);
        }

        return result;
    }

    /// <summary>
    /// 清理已被攻陷或改变归属的无效目标
    /// </summary>
    public static void CleanInvalidObjectives(WarState war, WorldTickContext ctx)
    {
        if (ctx.Pois == null) return;

        // A 的目标是夺取 B 的 POI，因此 ObjectivesA 里的 POI 其 OwningFaction 必须是 B 且不能是 A
        war.ObjectivesA = war.ObjectivesA.Where(name => 
        {
            var poi = ctx.Pois.FirstOrDefault(p => p.PoiName == name);
            return poi != null && poi.OwningFaction == war.NationB;
        }).ToList();

        // B 的目标是夺取 A 的 POI，因此 ObjectivesB 里的 POI 其 OwningFaction 必须是 A 且不能是 B
        war.ObjectivesB = war.ObjectivesB.Where(name => 
        {
            var poi = ctx.Pois.FirstOrDefault(p => p.PoiName == name);
            return poi != null && poi.OwningFaction == war.NationA;
        }).ToList();
    }
}
