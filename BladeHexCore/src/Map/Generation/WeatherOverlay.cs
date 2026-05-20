// WeatherOverlay.cs
// 大地图天气进入战斗地图的覆盖应用 — 服务于 spec combat-hex-from-overworld-state R7。
//
// 核心契约：
//   - "snow"      → Plains/Grassland/Savanna 25% 改写为 Snow
//   - "sandstorm" → Plains/Grassland/Savanna 25% 改写为 Sand
//   - "rain"      → 不改地形,仅设 mapData.EnvironmentEvent = "rain"
//   - "clear"/null/未列出 → 不动 + info 日志(便于将来扩展时发现漏 case)
//   - 不改写:Road / Wall / 水域 / Bridge / 据点建筑 cell
//   - EnvironmentOverride 优先于 weather override 设 EnvironmentEvent(R7#3)
//   - 用 SeededRng 选 cell,确定性输出(R7#5)
using System.Collections.Generic;
using BladeHex.Data;
using Godot;

namespace BladeHex.Map.Generation;

public static class WeatherOverlay
{
    /// <summary>R7 主入口。weatherOverride 取值 "clear"/"rain"/"snow"/"sandstorm"/null 等。</summary>
    public static void Apply(
        string? weatherOverride,
        Dictionary<Vector2I, BattleCellData.TerrainType> terrainMap,
        BattleMapGenerator.BattleMapData mapData,
        SeededRng rng)
    {
        if (string.IsNullOrEmpty(weatherOverride)) return;
        var w = weatherOverride.ToLowerInvariant();

        switch (w)
        {
            case "snow":
                OverwriteOpenTerrain(terrainMap, BattleCellData.TerrainType.Snow, 0.25f, rng);
                break;
            case "sandstorm":
                OverwriteOpenTerrain(terrainMap, BattleCellData.TerrainType.Sand, 0.25f, rng);
                break;
            case "rain":
                // R7#1:不改地形;仅在 EnvironmentEvent 为空时设为 "rain"
                // R7#3 EnvironmentOverride 优先 — 调用方在主流程末尾决定最终 EnvironmentEvent
                if (string.IsNullOrEmpty(mapData.EnvironmentEvent))
                    mapData.EnvironmentEvent = "rain";
                break;
            case "clear":
            case "":
                break;
            default:
                GD.Print($"[WeatherOverlay] 未识别的 weatherOverride: '{weatherOverride}',跳过");
                break;
        }
    }

    /// <summary>
    /// 把 Plains / Grassland / Savanna 三种"开阔地形"的 25% 改写为 newTerrain。
    /// 不改写 Road / Wall / 水域 / Bridge / 据点建筑。
    /// </summary>
    private static void OverwriteOpenTerrain(
        Dictionary<Vector2I, BattleCellData.TerrainType> terrainMap,
        BattleCellData.TerrainType newTerrain,
        float chance,
        SeededRng rng)
    {
        // 收集候选 cell（按 axial 字典序遍历,保证确定性)
        var candidates = new List<Vector2I>();
        foreach (var kv in terrainMap)
        {
            if (IsOpenTerrain(kv.Value))
                candidates.Add(kv.Key);
        }
        // 按 axial 字典序排序
        candidates.Sort((a, b) =>
        {
            int c = a.X.CompareTo(b.X);
            return c != 0 ? c : a.Y.CompareTo(b.Y);
        });

        foreach (var coord in candidates)
        {
            if (rng.NextBool(chance))
                terrainMap[coord] = newTerrain;
        }
    }

    /// <summary>"开阔地形":Plains / Grassland / Savanna。其它一律不被天气改写。</summary>
    private static bool IsOpenTerrain(BattleCellData.TerrainType t) =>
        t == BattleCellData.TerrainType.Plains
        || t == BattleCellData.TerrainType.Grassland
        || t == BattleCellData.TerrainType.Savanna;
}
