using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace BladeHex.Strategic.WorldEvents;

/// <summary>
/// 战争领主命令管理
/// </summary>
public static class WarLordOrders
{
    /// <summary>
    /// 为战争中的领主指派合适的攻势目标 POI
    /// </summary>
    public static void AssignLordToObjective(OverworldEntity lord, WarState war, List<string> objectives, List<OverworldEntity> allLords, int currentDay, List<OverworldPOI> allPois)
    {
        if (objectives == null || objectives.Count == 0)
        {
            lord.AssignedWarTargetPoiName = "";
            return;
        }

        // 1. 检查锁定期（5天内不换目标），且目标仍然有效（在目标列表中）
        if (!string.IsNullOrEmpty(lord.AssignedWarTargetPoiName) && 
            objectives.Contains(lord.AssignedWarTargetPoiName) && 
            (currentDay - lord.WarTargetAssignedDay < 5))
        {
            // 锁定期内且目标有效，继续执行当前命令，不进行重新分配
            return;
        }

        // 2. 选择新目标:优先级第一,距离第二
        string bestTarget = "";
        int bestPriority = 0;     // 2 = High, 1 = Normal
        float bestDistance = float.MaxValue;

        foreach (var objName in objectives)
        {
            var targetPoi = allPois.FirstOrDefault(p => p.PoiName == objName);
            if (targetPoi == null) continue;

            // 限制条件 A:距离必须 <= 1500px
            float dist = lord.Position.DistanceTo(targetPoi.Position);
            if (dist > 1500.0f) continue;

            // 限制条件 B:同 faction 已分配该目标的领主数 < 2(不计 lord 自己)
            int assignedCount = allLords.Count(l =>
                l.IsAlive &&
                l.Faction == lord.Faction &&
                l.AssignedWarTargetPoiName == objName &&
                l != lord);
            if (assignedCount >= 2) continue;

            // 优先级:objectives 前 2 个为 High (评分最高),其余为 Normal
            int idx = objectives.IndexOf(objName);
            int priority = idx < 2 ? 2 : 1;

            // 比较:先比优先级,再比距离
            bool better = priority > bestPriority
                || (priority == bestPriority && dist < bestDistance);
            if (better)
            {
                bestPriority = priority;
                bestDistance = dist;
                bestTarget = objName;
            }
        }

        if (!string.IsNullOrEmpty(bestTarget))
        {
            lord.AssignedWarTargetPoiName = bestTarget;
            lord.WarTargetAssignedDay = currentDay;
            GD.Print($"[WarLordOrders] 领主 {lord.EntityName} ({lord.Faction}) 被分配战争目标 POI: {bestTarget} (优先度: {bestPriority})，天数: {currentDay}");
        }
        else
        {
            // 如果无可分配目标，则置空
            lord.AssignedWarTargetPoiName = "";
        }
    }
}
