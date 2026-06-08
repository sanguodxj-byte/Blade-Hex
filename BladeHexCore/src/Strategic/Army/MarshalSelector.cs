using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic.Army;

public static class MarshalSelector
{
    private static int GetPersonalityWeight(OverworldPOI.LordPersonality personality)
    {
        return personality switch
        {
            OverworldPOI.LordPersonality.Aggressive => 3,
            OverworldPOI.LordPersonality.Balanced => 2,
            OverworldPOI.LordPersonality.Cautious => 1,
            _ => 0
        };
    }

    public static void SelectMarshalsForWars(WorldEventEngine engine, List<OverworldEntity> allLords, List<OverworldPOI> allPois, ArmyRegistry registry, int currentDay)
    {
        // 注：engine 也传递给 ProcessFactionObjective 以便推新闻
        if (engine == null || allLords == null || allPois == null || registry == null) return;

        foreach (var war in engine.ActiveWars)
        {
            // 分别处理攻守双方
            ProcessFactionObjective(war.NationA, war.ObjectivesA, allLords, allPois, registry, currentDay, engine);
            ProcessFactionObjective(war.NationB, war.ObjectivesB, allLords, allPois, registry, currentDay, engine);
        }
    }

    private static void ProcessFactionObjective(string faction, List<string> objectives, List<OverworldEntity> allLords, List<OverworldPOI> allPois, ArmyRegistry registry, int currentDay, WorldEventEngine? engine = null)
    {
        if (objectives == null) return;

        foreach (var targetName in objectives)
        {
            if (string.IsNullOrEmpty(targetName)) continue;

            // 1. 若该势力已有针对该目标的军团 -> 跳过
            bool targetAlreadyAssigned = registry.All().Any(a => 
                a.Faction == faction && 
                a.TargetPoiName == targetName &&
                a.State != ArmyState.Disbanding);
            if (targetAlreadyAssigned) continue;

            // 找到目标 POI
            var targetPoi = allPois.FirstOrDefault(p => p.PoiName == targetName);
            if (targetPoi == null) continue;

            // 2. 筛选候选元帅
            //    跳过:已属其他军团、LOD 休眠、距离过远、cooldown 期内未结束
            var candidates = allLords.Where(l =>
                l.IsAlive &&
                l.Faction == faction &&
                l.EntityTypeEnum == OverworldEntity.EntityType.LordArmy &&
                string.IsNullOrEmpty(l.ArmyId) &&
                l.Lod == OverworldEntity.EntityLod.Active &&
                l.MarshalCooldownUntilDay <= currentDay &&
                l.Position.DistanceTo(targetPoi.Position) <= 1500.0f
            ).ToList();

            if (candidates.Count > 0)
            {
                // 按性格降序，性格相同时按战力降序
                var marshal = candidates
                    .OrderByDescending(l => GetPersonalityWeight(l.LordPersonalityValue))
                    .ThenByDescending(l => l.CombatPower)
                    .First();

                // 创建临时军团
                registry.Create(marshal, targetName, currentDay);
                GD.Print($"[MarshalSelector] 为势力 {faction} 创建临时军团以攻打 {targetName}。元帅为: {marshal.EntityName} (性格: {marshal.LordPersonalityValue}, 战力: {marshal.CombatPower:F1})");

                // F1: 推送军团集结新闻
                engine?.AddNews("army_formed",
                    $"⚔ 【军团集结】{faction} 的 {marshal.EntityName} 奉命率众集结成军，目标: {targetName}，矢志夺城！",
                    marshal.Position);
            }
        }
    }
}
