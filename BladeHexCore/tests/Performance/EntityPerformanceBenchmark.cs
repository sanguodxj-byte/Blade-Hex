using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using BladeHex.Strategic;

namespace BladeHex.Tests.Performance;

/// <summary>
/// 性能基准测试：用于量化大地图空间重构前后的处理耗时与加速收益。
/// </summary>
public static class EntityPerformanceBenchmark
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        
        details.Add("=== 性能基准测试 ===");
        
        RunScenario(50, details);
        RunScenario(100, details);
        RunScenario(200, details);
        RunScenario(250, details);  // M6: 250 实体场景
        
        details.Add("====================");
        
        return (1, 0, details); // 性能测试作为辅助验证，始终返回 1 passed
    }

    private static void RunScenario(int entityCount, List<string> details)
    {
        var random = new Random(1337);
        var entities = new List<OverworldEntity>();
        for (int i = 0; i < entityCount; i++)
        {
            entities.Add(new OverworldEntity
            {
                EntityName = $"BenchEntity_{i}",
                Position = new Vector2((float)random.NextDouble() * 5000f, (float)random.NextDouble() * 5000f),
                IsAlive = true,
                VisionRange = 400f
            });
        }

        // 1. 建立空间索引
        var index = new EntitySpatialIndex(800f);
        index.Rebuild(entities);

        // 2. 测试 ProcessEntityInteractions 耗时
        var resolver = new BattleResolver();
        
        // 暴力方式 (跑 50 次取平均)
        var swBrute = Stopwatch.StartNew();
        for (int i = 0; i < 50; i++)
        {
            resolver.ProcessEntityInteractions(entities, null, null, null); // index 为 null
        }
        swBrute.Stop();
        double bruteMs = swBrute.Elapsed.TotalMilliseconds / 50.0;

        // 空间索引方式 (跑 100 次取平均)
        var swSpatial = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            resolver.ProcessEntityInteractions(entities, null, null, index);
        }
        swSpatial.Stop();
        double spatialMs = swSpatial.Elapsed.TotalMilliseconds / 100.0;

        // 3. 测试 GetVisibleEntities 耗时
        var swQuery = Stopwatch.StartNew();
        for (int i = 0; i < 200; i++)
        {
            var center = new Vector2((float)random.NextDouble() * 5000f, (float)random.NextDouble() * 5000f);
            var results = index.QueryRadius(center, 3000f);
            int count = 0;
            foreach (var e in results) count++;
        }
        swQuery.Stop();
        double queryMs = swQuery.Elapsed.TotalMilliseconds / 200.0;

        details.Add($"[规模: {entityCount} 实体]");
        details.Add($"  - BattleResolver (暴力 O(N^2)) 平均耗时: {bruteMs:F4} ms");
        details.Add($"  - BattleResolver (空间索引) 平均耗时: {spatialMs:F4} ms (加速比: {(bruteMs / (spatialMs > 0.00001 ? spatialMs : 0.00001)):F1}x)");
        details.Add($"  - GetVisibleEntities (空间索引) 平均检索耗时: {queryMs:F4} ms");
    }
}
