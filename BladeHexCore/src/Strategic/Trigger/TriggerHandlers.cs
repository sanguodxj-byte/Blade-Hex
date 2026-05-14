// TriggerHandlers.cs
// 五种触发 Handler 的具体实现
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

// ========================================
// 1. SpatialTriggerHandler — 空间触发
// ========================================

/// <summary>
/// 空间触发处理器 — 玩家进入新 Chunk 时判定遭遇、资源点等
/// </summary>
public class SpatialTriggerHandler : ITriggerHandler
{
    public TriggerType Type => TriggerType.Spatial;

    private readonly EncounterSpawner _spawner = new();

    public TriggerResult? Evaluate(TriggerCondition condition, TriggerContext ctx)
    {
        if (ctx.CurrentChunk == null) return null;

        // 区域过滤
        if (condition.RequiredRegions.Length > 0)
        {
            bool regionMatch = false;
            foreach (var r in condition.RequiredRegions)
                if (ctx.CurrentRegion == r) { regionMatch = true; break; }
            if (!regionMatch) return null;
        }

        // 地形过滤
        if (condition.RequiredTerrains.Length > 0)
        {
            bool terrainMatch = false;
            foreach (var t in condition.RequiredTerrains)
                if (ctx.CurrentTerrain == t) { terrainMatch = true; break; }
            if (!terrainMatch) return null;
        }

        // 确定性概率判定
        int hash = ctx.WorldSeed ^ (ctx.PlayerWorldPos.X * 7919 + ctx.PlayerWorldPos.Y * 104729);
        var rng = new Random(hash);
        float roll = (float)rng.NextDouble();

        // 危险等级修正
        float dangerMod = 1.0f + ctx.DangerLevel * 0.5f;
        // 天数修正
        float dayMod = 1.0f + Math.Min(ctx.CurrentDay * 0.005f, 0.3f);

        float finalChance = condition.Chance * dangerMod * dayMod;

        bool triggered = roll < finalChance;

        var result = new TriggerResult
        {
            TriggerId = condition.Id,
            Triggered = triggered,
            UnlockedIds = condition.UnlockedIds,
            Narrative = condition.Narrative,
        };

        if (triggered && ctx.CurrentChunk != null)
        {
            // 在 chunk 上标记遭遇槽位
            ctx.CurrentChunk.SetEncounterState(
                ctx.PlayerWorldPos.X, ctx.PlayerWorldPos.Y,
                Map.EncounterSlotState.Available
            );

            // 生成遭遇数据
            var tile = ctx.CurrentChunk.GetTile(ctx.PlayerWorldPos.X, ctx.PlayerWorldPos.Y);
            if (tile != null)
            {
                result.Encounter = _spawner.BuildEncounter(
                    ctx.PlayerWorldPos, tile, ctx.PlayerLevel, ctx.DangerLevel
                );
            }
        }

        return result;
    }
}

// ========================================
// 2. InteractionTriggerHandler — 交互触发
// ========================================

/// <summary>
/// 交互触发处理器 — 玩家主动接触时判定（确定性的，不做概率）
/// </summary>
public class InteractionTriggerHandler : ITriggerHandler
{
    public TriggerType Type => TriggerType.Interaction;

    public TriggerResult? Evaluate(TriggerCondition condition, TriggerContext ctx)
    {
        // 交互触发不做概率判定 — 确定性触发
        // 只检查前置条件（由 TriggerEngine.CheckPrerequisites 处理）

        var result = new TriggerResult
        {
            TriggerId = condition.Id,
            Triggered = true,
            UnlockedIds = condition.UnlockedIds,
            Narrative = condition.Narrative,
        };

        return result;
    }
}

// ========================================
// 3. TimeTriggerHandler — 时间触发
// ========================================

/// <summary>
/// 时间触发处理器 — 天数推进时判定 POI 变化
/// </summary>
public class TimeTriggerHandler : ITriggerHandler
{
    public TriggerType Type => TriggerType.Time;

    /// <summary>关联的 POI 列表（由外部注入）</summary>
    public List<OverworldPOI> Pois { get; set; } = new();

    public TriggerResult? Evaluate(TriggerCondition condition, TriggerContext ctx)
    {
        // 时间触发对 POI 做批量操作
        // 每个 condition 对应一类 POI 事件

        bool anyTriggered = false;

        foreach (var poi in Pois)
        {
            // POI 天数推进
            poi.OnDayPassed();

            // 掠夺队刷新检查
            if (condition.Id == "time_raid_spawn" && poi.ShouldSpawnRaidParty())
            {
                anyTriggered = true;
                poi.OnRaidPartySpawned();
            }

            // 繁荣度恢复
            if (condition.Id == "time_poi_recovery" && poi.Prosperity < 50 && !poi.IsUnderSiege)
            {
                anyTriggered = true;
            }
        }

        return new TriggerResult
        {
            TriggerId = condition.Id,
            Triggered = anyTriggered,
            UnlockedIds = condition.UnlockedIds,
            Narrative = condition.Narrative,
        };
    }
}

// ========================================
// 4. ChainTriggerHandler — 连锁触发
// ========================================

/// <summary>
/// 连锁触发处理器 — 前置事件完成后判定
/// </summary>
public class ChainTriggerHandler : ITriggerHandler
{
    public TriggerType Type => TriggerType.Chain;

    public TriggerResult? Evaluate(TriggerCondition condition, TriggerContext ctx)
    {
        // 前置条件检查已由 TriggerEngine.CheckPrerequisites 完成
        // 连锁触发是确定性的 — 前置满足即触发

        return new TriggerResult
        {
            TriggerId = condition.Id,
            Triggered = true,
            UnlockedIds = condition.UnlockedIds,
            Narrative = condition.Narrative,
        };
    }
}

// ========================================
// 5. EnvironmentTriggerHandler — 环境触发
// ========================================

/// <summary>
/// 环境触发处理器 — 地形 + 季节 + 天数综合判定
/// </summary>
public class EnvironmentTriggerHandler : ITriggerHandler
{
    public TriggerType Type => TriggerType.Environment;

    /// <summary>环境事件类型</summary>
    public enum EnvironmentEvent
    {
        None,
        Blizzard,     // 暴风雪 — 雪地/针叶林 + 冬季
        Sandstorm,    // 沙暴 — 荒漠 + 春秋
        Flood,        // 洪水 — 沼泽/河流 + 夏季
        Landslide,    // 山崩 — 山地/丘陵 + 雨季
        ForestFire,   // 森林火灾 — 密林 + 夏季干旱
    }

    public TriggerResult? Evaluate(TriggerCondition condition, TriggerContext ctx)
    {
        // 季节判定（基于天数，30天/月，4月/季）
        int month = ((ctx.CurrentDay / 30) % 12) + 1;
        string season = month switch
        {
            >= 3 and <= 5 => "spring",
            >= 6 and <= 8 => "summer",
            >= 9 and <= 11 => "autumn",
            _ => "winter",
        };

        // 地形 + 季节 → 环境事件概率
        var envEvent = DetermineEnvironmentEvent(ctx.CurrentTerrain, season, ctx.DangerLevel);

        if (envEvent == EnvironmentEvent.None) return null;

        // 概率判定
        int hash = ctx.WorldSeed ^ (ctx.CurrentDay * 31 + ctx.PlayerChunk.X * 17 + ctx.PlayerChunk.Y);
        var rng = new Random(hash);
        float chance = condition.Chance * GetSeasonModifier(season);
        bool triggered = (float)rng.NextDouble() < chance;

        if (!triggered) return null;

        string narrative = envEvent switch
        {
            EnvironmentEvent.Blizzard => "一场暴风雪席卷而来，视野急剧下降！",
            EnvironmentEvent.Sandstorm => "沙暴遮天蔽日，行进艰难！",
            EnvironmentEvent.Flood => "河水暴涨，低洼地区被淹没！",
            EnvironmentEvent.Landslide => "山石崩落，道路受阻！",
            EnvironmentEvent.ForestFire => "浓烟升起，森林大火正在蔓延！",
            _ => "",
        };

        return new TriggerResult
        {
            TriggerId = condition.Id,
            Triggered = true,
            Narrative = narrative,
            UnlockedIds = condition.UnlockedIds,
        };
    }

    /// <summary>根据地形和季节判定环境事件</summary>
    private EnvironmentEvent DetermineEnvironmentEvent(
        Map.HexOverworldTile.TerrainType terrain, string season, float dangerLevel)
    {
        return terrain switch
        {
            Map.HexOverworldTile.TerrainType.Snow or Map.HexOverworldTile.TerrainType.Taiga
                when season == "winter" => EnvironmentEvent.Blizzard,
            Map.HexOverworldTile.TerrainType.Sand or Map.HexOverworldTile.TerrainType.Savanna
                when season == "spring" || season == "autumn" => EnvironmentEvent.Sandstorm,
            Map.HexOverworldTile.TerrainType.Swamp
                when season == "summer" => EnvironmentEvent.Flood,
            Map.HexOverworldTile.TerrainType.Hills or Map.HexOverworldTile.TerrainType.Mountain
                when season == "summer" => EnvironmentEvent.Landslide,
            Map.HexOverworldTile.TerrainType.DenseForest
                when season == "summer" && dangerLevel > 0.5f => EnvironmentEvent.ForestFire,
            _ => EnvironmentEvent.None,
        };
    }

    /// <summary>季节对事件概率的修正</summary>
    private static float GetSeasonModifier(string season) => season switch
    {
        "winter" => 1.3f,
        "summer" => 1.1f,
        "spring" => 0.8f,
        "autumn" => 0.9f,
        _ => 1.0f,
    };
}
