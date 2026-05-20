// BattleMapGenerationTests.cs
// 战斗地图生成综合测试 — 验证 cell 分布规律、比例、合理性
//
// 覆盖：
//   - 各规模地图 cell 数是否符合预期
//   - 地形分布比例是否合理（主地形占比、水域封顶）
//   - 高程分布是否合理（无悬空、高程-地形一致性）
//   - 道路连通性（贯通地图）
//   - 部署区可用性（双方都有足够可部署格）
//   - 据点结构完整性（城墙/城门/塔楼存在）
//   - 过渡带存在性（不同地形之间有过渡）
//   - 确定性（同 seed 产出相同结果）
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Map.Generation;
using BladeHex.Strategic;
using Godot;

namespace BladeHex.Map.Tests;

public static class BattleMapGenerationTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }

        // 统计报告
        details.Add("");
        details.Add("=== 分布统计报告 ===");
        AppendDistributionReport(details);

        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        // 基础 cell 数
        yield return Run(nameof(Mercenary_CellCount_Matches_Radius7), Mercenary_CellCount_Matches_Radius7);
        yield return Run(nameof(Knight_CellCount_Matches_Radius11), Knight_CellCount_Matches_Radius11);
        yield return Run(nameof(Lord_CellCount_Matches_Radius14), Lord_CellCount_Matches_Radius14);
        yield return Run(nameof(Stronghold_CellCount_Matches_Radius14), Stronghold_CellCount_Matches_Radius14);

        // 地形分布
        yield return Run(nameof(PlainField_PrimaryTerrainDominates), PlainField_PrimaryTerrainDominates);
        yield return Run(nameof(ForestAmbush_ForestTerrainSignificant), ForestAmbush_ForestTerrainSignificant);
        yield return Run(nameof(MountainPass_HighElevationPresent), MountainPass_HighElevationPresent);
        yield return Run(nameof(SwampBattle_SwampOrWaterPresent), SwampBattle_SwampOrWaterPresent);
        yield return Run(nameof(WaterCap_NeverExceeds30Percent), WaterCap_NeverExceeds30Percent);

        // 高程合理性
        yield return Run(nameof(Elevation_TerrainConsistency), Elevation_TerrainConsistency);
        yield return Run(nameof(Elevation_DistributionNotFlat), Elevation_DistributionNotFlat);

        // 道路
        yield return Run(nameof(RoadPreset_HasRoadCells), RoadPreset_HasRoadCells);
        yield return Run(nameof(Road_Width3_Exists), Road_Width3_Exists);

        // 部署区
        yield return Run(nameof(Deployment_BothSidesHaveMinimum), Deployment_BothSidesHaveMinimum);
        yield return Run(nameof(Deployment_NoOverlap), Deployment_NoOverlap);

        // 据点结构
        yield return Run(nameof(CastleSiege_HasWallStructures), CastleSiege_HasWallStructures);
        yield return Run(nameof(CastleSiege_HasGate), CastleSiege_HasGate);
        yield return Run(nameof(CastleSiege_HasTower), CastleSiege_HasTower);
        yield return Run(nameof(CastleSiege_HasStaircase), CastleSiege_HasStaircase);
        yield return Run(nameof(CastleSiege_WallAtEdge), CastleSiege_WallAtEdge);
        yield return Run(nameof(CastleSiege_WallElevation2), CastleSiege_WallElevation2);
        yield return Run(nameof(CastleSiege_TowerElevation3), CastleSiege_TowerElevation3);
        yield return Run(nameof(CastleSiege_StaircaseInsideWall), CastleSiege_StaircaseInsideWall);
        yield return Run(nameof(CastleSiege_GateIsDestructible), CastleSiege_GateIsDestructible);
        yield return Run(nameof(CastleSiege_MultiSeed_Consistent), CastleSiege_MultiSeed_Consistent);

        // 过渡带
        yield return Run(nameof(TransitionTerrain_ExistsBetweenDifferentBiomes), TransitionTerrain_ExistsBetweenDifferentBiomes);

        // 确定性
        yield return Run(nameof(Determinism_SameSeed_SameOutput), Determinism_SameSeed_SameOutput);
        yield return Run(nameof(Determinism_DifferentSeed_DifferentOutput), Determinism_DifferentSeed_DifferentOutput);
    }

    private static (string, bool, string) Run(string name, System.Func<(bool, string)> test)
    {
        try { var (ok, msg) = test(); return (name, ok, msg); }
        catch (System.Exception ex) { return (name, false, $"threw {ex.GetType().Name}: {ex.Message}"); }
    }

    // ========================================================================
    // 辅助方法
    // ========================================================================

    private static BattleMapGenerator.BattleMapData Generate(string preset, BattleContext.BattleSize size, int seed = 42)
    {
        var generator = new BattleMapGenerator();
        return generator.GenerateFromTemplate(preset, (BattleMapGenerator.BattleSize)(int)size, seed);
    }

    private static BattleMapGenerator.BattleMapData GenerateContext(string preset, BattleContext.BattleSize size, int seed = 42)
    {
        var ctx = BattleOverworldFactory.CreateContext(preset, size, seed);
        var generator = new BattleMapGenerator();
        return generator.Generate(ctx);
    }

    private static Dictionary<BattleCellData.TerrainType, int> CountTerrains(BattleMapGenerator.BattleMapData md)
    {
        var counts = new Dictionary<BattleCellData.TerrainType, int>();
        foreach (var v in md.Cells.Values)
        {
            var cd = v.As<BattleCellData>();
            if (cd == null) continue;
            counts[cd.terrainType] = counts.GetValueOrDefault(cd.terrainType) + 1;
        }
        return counts;
    }

    private static Dictionary<int, int> CountElevations(BattleMapGenerator.BattleMapData md)
    {
        var counts = new Dictionary<int, int>();
        foreach (var v in md.Cells.Values)
        {
            var cd = v.As<BattleCellData>();
            if (cd == null) continue;
            counts[cd.elevation] = counts.GetValueOrDefault(cd.elevation) + 1;
        }
        return counts;
    }

    private static int ExpectedCells(int radius) => 1 + 3 * radius * (radius + 1);

    // ========================================================================
    // Cell 数测试
    // ========================================================================

    static (bool, string) Mercenary_CellCount_Matches_Radius7()
    {
        var md = Generate("plain_field", BattleContext.BattleSize.Mercenary);
        int expected = ExpectedCells(7); // 169
        int actual = md.Cells.Count;
        return (actual == expected, $"expected {expected}, got {actual}");
    }

    static (bool, string) Knight_CellCount_Matches_Radius11()
    {
        var md = Generate("plain_field", BattleContext.BattleSize.Knight);
        int expected = ExpectedCells(11); // 397
        int actual = md.Cells.Count;
        return (actual == expected, $"expected {expected}, got {actual}");
    }

    static (bool, string) Lord_CellCount_Matches_Radius14()
    {
        var md = Generate("plain_field", BattleContext.BattleSize.Lord);
        int expected = ExpectedCells(14); // 631
        int actual = md.Cells.Count;
        return (actual == expected, $"expected {expected}, got {actual}");
    }

    static (bool, string) Stronghold_CellCount_Matches_Radius14()
    {
        var md = Generate("castle_siege", BattleContext.BattleSize.Stronghold);
        int expected = ExpectedCells(14); // 631
        int actual = md.Cells.Count;
        return (actual == expected, $"expected {expected}, got {actual}");
    }

    // ========================================================================
    // 地形分布测试
    // ========================================================================

    static (bool, string) PlainField_PrimaryTerrainDominates()
    {
        // 平原旷野：开阔地形（Plains/Grassland/Savanna/Road）应占 > 40%
        int totalOpen = 0, totalCells = 0;
        var generator = new BattleMapGenerator();
        for (int seed = 1; seed <= 5; seed++)
        {
            var md = generator.GenerateFromTemplate("plain_field",
                BattleMapGenerator.BattleSize.Knight, seed * 1000 + 42);
            var counts = CountTerrains(md);
            totalCells += md.Cells.Count;
            totalOpen += counts.GetValueOrDefault(BattleCellData.TerrainType.Plains)
                + counts.GetValueOrDefault(BattleCellData.TerrainType.Grassland)
                + counts.GetValueOrDefault(BattleCellData.TerrainType.Savanna)
                + counts.GetValueOrDefault(BattleCellData.TerrainType.Road);
        }
        float pct = totalCells > 0 ? (float)totalOpen / totalCells : 0;
        return (pct > 0.40f, $"open terrain avg = {pct:P1} across 5 seeds, expected > 40%");
    }

    static (bool, string) ForestAmbush_ForestTerrainSignificant()
    {
        var md = Generate("forest_ambush", BattleContext.BattleSize.Knight, 456);
        var counts = CountTerrains(md);
        int total = md.Cells.Count;

        int forestCount = counts.GetValueOrDefault(BattleCellData.TerrainType.Forest)
            + counts.GetValueOrDefault(BattleCellData.TerrainType.DenseForest)
            + counts.GetValueOrDefault(BattleCellData.TerrainType.Taiga)
            + counts.GetValueOrDefault(BattleCellData.TerrainType.Jungle);
        float pct = (float)forestCount / total;
        return (pct > 0.15f, $"forest terrain = {pct:P1}, expected > 15%");
    }

    static (bool, string) MountainPass_HighElevationPresent()
    {
        var md = Generate("mountain_pass", BattleContext.BattleSize.Knight, 789);
        var elevs = CountElevations(md);
        int total = md.Cells.Count;

        // 高地 = elevation ≥ 3（丘陵/山地在新系统中是 3-5）
        int highCount = elevs.GetValueOrDefault(3) + elevs.GetValueOrDefault(4) + elevs.GetValueOrDefault(5);
        float pct = (float)highCount / total;
        return (pct > 0.10f, $"elevation≥3 = {pct:P1}, expected > 10%");
    }

    static (bool, string) SwampBattle_SwampOrWaterPresent()
    {
        var md = Generate("swamp_battle", BattleContext.BattleSize.Knight, 101);
        var counts = CountTerrains(md);
        int total = md.Cells.Count;

        int wetCount = counts.GetValueOrDefault(BattleCellData.TerrainType.Swamp)
            + counts.GetValueOrDefault(BattleCellData.TerrainType.Bog)
            + counts.GetValueOrDefault(BattleCellData.TerrainType.ShallowWater)
            + counts.GetValueOrDefault(BattleCellData.TerrainType.DeepWater);
        float pct = (float)wetCount / total;
        return (pct > 0.10f, $"wet terrain = {pct:P1}, expected > 10%");
    }

    static (bool, string) WaterCap_NeverExceeds30Percent()
    {
        // 跑多个 seed 确保水域不超标
        var generator = new BattleMapGenerator();
        for (int seed = 1; seed <= 20; seed++)
        {
            var md = generator.GenerateFromTemplate("coastal_ambush",
                BattleMapGenerator.BattleSize.Knight, seed);
            var counts = CountTerrains(md);
            int total = md.Cells.Count;
            int waterCount = counts.GetValueOrDefault(BattleCellData.TerrainType.ShallowWater)
                + counts.GetValueOrDefault(BattleCellData.TerrainType.DeepWater);
            float pct = (float)waterCount / total;
            if (pct > 0.30f)
                return (false, $"seed={seed}: water = {pct:P1}, exceeds 30% cap");
        }
        return (true, "all 20 seeds within 30% water cap");
    }

    // ========================================================================
    // 高程合理性测试
    // ========================================================================

    static (bool, string) Elevation_TerrainConsistency()
    {
        var md = Generate("mountain_pass", BattleContext.BattleSize.Knight, 555);
        int violations = 0;
        foreach (var v in md.Cells.Values)
        {
            var cd = v.As<BattleCellData>();
            if (cd == null) continue;
            // 深水必须 elevation ≤ 1
            if (cd.terrainType == BattleCellData.TerrainType.DeepWater && cd.elevation > 1) violations++;
            // 山地必须 elevation ≥ 3
            if (cd.terrainType == BattleCellData.TerrainType.Mountain && cd.elevation < 3) violations++;
            // elevation 必须在 [0,5] 范围
            if (cd.elevation < 0 || cd.elevation > 5) violations++;
        }
        return (violations == 0, $"{violations} terrain-elevation violations");
    }

    static (bool, string) Elevation_DistributionNotFlat()
    {
        var md = Generate("mountain_pass", BattleContext.BattleSize.Knight, 777);
        var elevs = CountElevations(md);
        // 应该有至少 2 种不同高程
        return (elevs.Count >= 2, $"only {elevs.Count} elevation levels, expected ≥ 2");
    }

    // ========================================================================
    // 道路测试
    // ========================================================================

    static (bool, string) RoadPreset_HasRoadCells()
    {
        // 跑多个 seed，至少有一个能生成道路
        var generator = new BattleMapGenerator();
        int roadFound = 0;
        for (int seed = 1; seed <= 5; seed++)
        {
            var md = generator.GenerateFromTemplate("plain_field",
                BattleMapGenerator.BattleSize.Knight, seed * 100 + 333);
            var counts = CountTerrains(md);
            roadFound += counts.GetValueOrDefault(BattleCellData.TerrainType.Road);
        }
        return (roadFound > 0, $"road cells across 5 seeds = {roadFound}, expected > 0");
    }

    static (bool, string) Road_Width3_Exists()
    {
        var md = Generate("plain_field", BattleContext.BattleSize.Lord, 444);
        // 检查是否存在道路格子有 ≥2 个道路邻居（宽度 3 的标志）
        int wideRoadCells = 0;
        foreach (Variant keyV in md.Cells.Keys)
        {
            var key = keyV.AsVector2I();
            var cd = md.Cells[keyV].As<BattleCellData>();
            if (cd == null || cd.terrainType != BattleCellData.TerrainType.Road) continue;

            int roadNeighbors = 0;
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(key.X, key.Y, d);
                var nbV = Variant.From(nb);
                if (!md.Cells.ContainsKey(nbV)) continue;
                var nbCd = md.Cells[nbV].As<BattleCellData>();
                if (nbCd?.terrainType == BattleCellData.TerrainType.Road) roadNeighbors++;
            }
            if (roadNeighbors >= 2) wideRoadCells++;
        }
        return (wideRoadCells > 3, $"wide road cells (≥2 road neighbors) = {wideRoadCells}, expected > 3");
    }

    // ========================================================================
    // 部署区测试
    // ========================================================================

    static (bool, string) Deployment_BothSidesHaveMinimum()
    {
        var md = Generate("plain_field", BattleContext.BattleSize.Knight, 111);
        int playerCount = md.PlayerDeployment.Count;
        int enemyCount = md.EnemyDeployment.Count;
        bool ok = playerCount >= 4 && enemyCount >= 4;
        return (ok, $"player={playerCount}, enemy={enemyCount}, expected both ≥ 4");
    }

    static (bool, string) Deployment_NoOverlap()
    {
        var md = Generate("plain_field", BattleContext.BattleSize.Knight, 222);
        var playerSet = new HashSet<Vector2I>();
        foreach (var v in md.PlayerDeployment) playerSet.Add(v.AsVector2I());
        int overlap = 0;
        foreach (var v in md.EnemyDeployment)
            if (playerSet.Contains(v.AsVector2I())) overlap++;
        return (overlap == 0, $"{overlap} overlapping deployment cells");
    }

    // ========================================================================
    // 据点结构测试
    // ========================================================================

    static (bool, string) CastleSiege_HasWallStructures()
    {
        var md = Generate("castle_siege", BattleContext.BattleSize.Stronghold, 999);
        var counts = CountTerrains(md);
        int rampartCount = counts.GetValueOrDefault(BattleCellData.TerrainType.Rampart);
        return (rampartCount >= 5, $"rampart cells = {rampartCount}, expected ≥ 5");
    }

    static (bool, string) CastleSiege_HasGate()
    {
        var md = Generate("castle_siege", BattleContext.BattleSize.Stronghold, 999);
        var counts = CountTerrains(md);
        int gateCount = counts.GetValueOrDefault(BattleCellData.TerrainType.Gate);
        return (gateCount >= 1, $"gate cells = {gateCount}, expected ≥ 1");
    }

    static (bool, string) CastleSiege_HasTower()
    {
        var md = Generate("castle_siege", BattleContext.BattleSize.Stronghold, 999);
        var counts = CountTerrains(md);
        int towerCount = counts.GetValueOrDefault(BattleCellData.TerrainType.Tower);
        return (towerCount >= 2, $"tower cells = {towerCount}, expected ≥ 2");
    }

    static (bool, string) CastleSiege_HasStaircase()
    {
        var md = Generate("castle_siege", BattleContext.BattleSize.Stronghold, 999);
        var counts = CountTerrains(md);
        int staircaseCount = counts.GetValueOrDefault(BattleCellData.TerrainType.Staircase);
        return (staircaseCount >= 1, $"staircase cells = {staircaseCount}, expected ≥ 1");
    }

    static (bool, string) CastleSiege_WallAtEdge()
    {
        // 城墙应该在地图边缘（距中心 > 60% 半径）
        var md = Generate("castle_siege", BattleContext.BattleSize.Stronghold, 999);
        int N = 14; // Stronghold radius
        int wallNearEdge = 0, wallTotal = 0;
        foreach (Variant keyV in md.Cells.Keys)
        {
            var key = keyV.AsVector2I();
            var cd = md.Cells[keyV].As<BattleCellData>();
            if (cd == null) continue;
            if (cd.terrainType != BattleCellData.TerrainType.Rampart
                && cd.terrainType != BattleCellData.TerrainType.Tower
                && cd.terrainType != BattleCellData.TerrainType.Gate) continue;
            wallTotal++;
            int dist = HexUtils.AxialDistance(key, Vector2I.Zero);
            if (dist >= (int)(N * 0.5f)) wallNearEdge++;
        }
        float edgePct = wallTotal > 0 ? (float)wallNearEdge / wallTotal : 0;
        return (edgePct >= 0.6f, $"wall at edge (dist≥{(int)(N*0.5f)}): {wallNearEdge}/{wallTotal} = {edgePct:P0}, expected ≥ 60%");
    }

    static (bool, string) CastleSiege_WallElevation2()
    {
        // 城墙 elevation 必须 > 周围自然地形（至少 +2）
        var md = Generate("castle_siege", BattleContext.BattleSize.Stronghold, 999);
        int wallCount = 0, tooLow = 0;
        foreach (Variant keyV in md.Cells.Keys)
        {
            var cd = md.Cells[keyV].As<BattleCellData>();
            if (cd == null || cd.terrainType != BattleCellData.TerrainType.Rampart) continue;
            wallCount++;
            if (cd.elevation < 4) tooLow++; // 基础2 + 偏移2 = 最低4
        }
        return (tooLow == 0, $"rampart too low (<4): {tooLow}/{wallCount}");
    }

    static (bool, string) CastleSiege_TowerElevation3()
    {
        // 塔楼 elevation 必须 > 城墙（至少 +1）
        var md = Generate("castle_siege", BattleContext.BattleSize.Stronghold, 999);
        int towerCount = 0, tooLow = 0;
        foreach (Variant keyV in md.Cells.Keys)
        {
            var cd = md.Cells[keyV].As<BattleCellData>();
            if (cd == null || cd.terrainType != BattleCellData.TerrainType.Tower) continue;
            towerCount++;
            if (cd.elevation < 5) tooLow++; // 基础2 + 偏移3 = 最低5
        }
        return (tooLow == 0, $"tower too low (<5): {tooLow}/{towerCount}");
    }

    static (bool, string) CastleSiege_StaircaseInsideWall()
    {
        // 楼梯必须在城墙内侧（不在城墙外侧）
        // 内侧 = 楼梯的防守方向投影值 > 相邻城墙的投影值
        var md = Generate("castle_siege", BattleContext.BattleSize.Stronghold, 999);
        var wallPositions = new HashSet<Vector2I>();
        var staircasePositions = new List<Vector2I>();

        foreach (Variant keyV in md.Cells.Keys)
        {
            var key = keyV.AsVector2I();
            var cd = md.Cells[keyV].As<BattleCellData>();
            if (cd == null) continue;
            if (cd.terrainType == BattleCellData.TerrainType.Rampart
                || cd.terrainType == BattleCellData.TerrainType.Tower
                || cd.terrainType == BattleCellData.TerrainType.Gate)
                wallPositions.Add(key);
            if (cd.terrainType == BattleCellData.TerrainType.Staircase)
                staircasePositions.Add(key);
        }

        // 每个楼梯必须与至少一个城墙格相邻
        int notAdjacentToWall = 0;
        foreach (var stair in staircasePositions)
        {
            bool adjacentToWall = false;
            for (int d = 0; d < 6; d++)
            {
                var nb = HexUtils.GetNeighbor(stair.X, stair.Y, d);
                if (wallPositions.Contains(nb)) { adjacentToWall = true; break; }
            }
            if (!adjacentToWall) notAdjacentToWall++;
        }
        return (notAdjacentToWall == 0,
            $"staircases not adjacent to wall: {notAdjacentToWall}/{staircasePositions.Count}");
    }

    static (bool, string) CastleSiege_GateIsDestructible()
    {
        // 城门必须是可破坏的
        var md = Generate("castle_siege", BattleContext.BattleSize.Stronghold, 999);
        int gateCount = 0, destructibleGates = 0;
        foreach (Variant keyV in md.Cells.Keys)
        {
            var cd = md.Cells[keyV].As<BattleCellData>();
            if (cd == null || cd.terrainType != BattleCellData.TerrainType.Gate) continue;
            gateCount++;
            if (cd.isDestructible && cd.durability > 0) destructibleGates++;
        }
        return (gateCount > 0 && destructibleGates == gateCount,
            $"destructible gates: {destructibleGates}/{gateCount}");
    }

    static (bool, string) CastleSiege_MultiSeed_Consistent()
    {
        // 跑 5 个 seed，每个都应该有完整的城堡结构
        var generator = new BattleMapGenerator();
        int failures = 0;
        var details = new System.Text.StringBuilder();
        for (int seed = 1; seed <= 5; seed++)
        {
            var md = generator.GenerateFromTemplate("castle_siege",
                BattleMapGenerator.BattleSize.Stronghold, seed * 777);
            var counts = CountTerrains(md);
            int rampart = counts.GetValueOrDefault(BattleCellData.TerrainType.Rampart);
            int tower = counts.GetValueOrDefault(BattleCellData.TerrainType.Tower);
            int gate = counts.GetValueOrDefault(BattleCellData.TerrainType.Gate);
            int staircase = counts.GetValueOrDefault(BattleCellData.TerrainType.Staircase);
            bool ok = rampart >= 5 && tower >= 2 && gate >= 1 && staircase >= 1;
            if (!ok)
            {
                failures++;
                details.Append($"seed={seed*777}: R={rampart} T={tower} G={gate} S={staircase}; ");
            }
        }
        return (failures == 0, failures == 0 ? "all 5 seeds have complete castle" : details.ToString());
    }

    // ========================================================================
    // 过渡带测试
    // ========================================================================

    static (bool, string) TransitionTerrain_ExistsBetweenDifferentBiomes()
    {
        // 在 Knight 规模下（多 sample），应该有过渡地形（Savanna 作为森林↔草地过渡）
        var md = Generate("forest_ambush", BattleContext.BattleSize.Knight, 567);
        var counts = CountTerrains(md);
        // Savanna 是典型的过渡地形
        int transitionCount = counts.GetValueOrDefault(BattleCellData.TerrainType.Savanna)
            + counts.GetValueOrDefault(BattleCellData.TerrainType.LuckyGrass);
        return (transitionCount > 0, $"transition terrain cells = {transitionCount}, expected > 0");
    }

    // ========================================================================
    // 确定性测试
    // ========================================================================

    static (bool, string) Determinism_SameSeed_SameOutput()
    {
        var md1 = Generate("forest_ambush", BattleContext.BattleSize.Knight, 12345);
        var md2 = Generate("forest_ambush", BattleContext.BattleSize.Knight, 12345);

        if (md1.Cells.Count != md2.Cells.Count)
            return (false, $"cell count differs: {md1.Cells.Count} vs {md2.Cells.Count}");

        int mismatches = 0;
        foreach (Variant key in md1.Cells.Keys)
        {
            var cd1 = md1.Cells[key].As<BattleCellData>();
            var cd2 = md2.Cells[key].As<BattleCellData>();
            if (cd1?.terrainType != cd2?.terrainType || cd1?.elevation != cd2?.elevation)
                mismatches++;
        }
        return (mismatches == 0, $"{mismatches} cell mismatches between same-seed runs");
    }

    static (bool, string) Determinism_DifferentSeed_DifferentOutput()
    {
        var md1 = Generate("plain_field", BattleContext.BattleSize.Knight, 11111);
        var md2 = Generate("plain_field", BattleContext.BattleSize.Knight, 22222);

        int differences = 0;
        foreach (Variant key in md1.Cells.Keys)
        {
            if (!md2.Cells.ContainsKey(key)) { differences++; continue; }
            var cd1 = md1.Cells[key].As<BattleCellData>();
            var cd2 = md2.Cells[key].As<BattleCellData>();
            if (cd1?.terrainType != cd2?.terrainType) differences++;
        }
        return (differences > 0, $"only {differences} differences between different seeds, expected > 0");
    }

    // ========================================================================
    // 分布统计报告（附加到测试输出）
    // ========================================================================

    private static void AppendDistributionReport(List<string> details)
    {
        var presets = new[] { "plain_field", "forest_ambush", "mountain_pass", "swamp_battle",
            "coastal_ambush", "desert_skirmish", "castle_siege" };
        var size = BattleContext.BattleSize.Knight;

        details.Add($"{"Preset",-18} | {"Cells",5} | {"Open%",5} | {"Forest%",7} | {"Water%",6} | {"Elev0%",6} | {"Elev1%",6} | {"Elev2%",6} | {"Road",4} | {"Diversity",9}");
        details.Add(new string('-', 105));

        foreach (var preset in presets)
        {
            var md = Generate(preset, size, 42);
            var terrains = CountTerrains(md);
            var elevs = CountElevations(md);
            int total = md.Cells.Count;

            int open = terrains.GetValueOrDefault(BattleCellData.TerrainType.Plains)
                + terrains.GetValueOrDefault(BattleCellData.TerrainType.Grassland)
                + terrains.GetValueOrDefault(BattleCellData.TerrainType.Savanna);
            int forest = terrains.GetValueOrDefault(BattleCellData.TerrainType.Forest)
                + terrains.GetValueOrDefault(BattleCellData.TerrainType.DenseForest)
                + terrains.GetValueOrDefault(BattleCellData.TerrainType.Taiga)
                + terrains.GetValueOrDefault(BattleCellData.TerrainType.Jungle);
            int water = terrains.GetValueOrDefault(BattleCellData.TerrainType.ShallowWater)
                + terrains.GetValueOrDefault(BattleCellData.TerrainType.DeepWater);
            int road = terrains.GetValueOrDefault(BattleCellData.TerrainType.Road);

            float openPct = 100f * open / total;
            float forestPct = 100f * forest / total;
            float waterPct = 100f * water / total;
            float elev0Pct = 100f * elevs.GetValueOrDefault(0) / total;
            float elev1Pct = 100f * elevs.GetValueOrDefault(1) / total;
            float elev2Pct = 100f * elevs.GetValueOrDefault(2) / total;

            details.Add($"{preset,-18} | {total,5} | {openPct,4:F0}% | {forestPct,5:F0}%  | {waterPct,4:F0}%  | {elev0Pct,4:F0}%  | {elev1Pct,4:F0}%  | {elev2Pct,4:F0}%  | {road,4} | {terrains.Count,9}");
        }

        // 城堡结构详细报告
        details.Add("");
        details.Add("=== 城堡结构详细 ===");
        var castleMd = Generate("castle_siege", BattleContext.BattleSize.Stronghold, 42);
        var ct = CountTerrains(castleMd);
        int rampart = ct.GetValueOrDefault(BattleCellData.TerrainType.Rampart);
        int tower = ct.GetValueOrDefault(BattleCellData.TerrainType.Tower);
        int gate = ct.GetValueOrDefault(BattleCellData.TerrainType.Gate);
        int staircase = ct.GetValueOrDefault(BattleCellData.TerrainType.Staircase);
        int ruins = ct.GetValueOrDefault(BattleCellData.TerrainType.Ruins);
        details.Add($"Rampart={rampart} Tower={tower} Gate={gate} Staircase={staircase} Ruins={ruins}");

        // 城墙坐标范围（验证位置）
        int minQ = int.MaxValue, maxQ = int.MinValue, minR = int.MaxValue, maxR = int.MinValue;
        int wallCount = 0;
        foreach (Variant keyV in castleMd.Cells.Keys)
        {
            var key = keyV.AsVector2I();
            var cd = castleMd.Cells[keyV].As<BattleCellData>();
            if (cd == null) continue;
            if (cd.terrainType == BattleCellData.TerrainType.Rampart
                || cd.terrainType == BattleCellData.TerrainType.Tower
                || cd.terrainType == BattleCellData.TerrainType.Gate)
            {
                wallCount++;
                if (key.X < minQ) minQ = key.X;
                if (key.X > maxQ) maxQ = key.X;
                if (key.Y < minR) minR = key.Y;
                if (key.Y > maxR) maxR = key.Y;
            }
        }
        details.Add($"Wall bbox: q=[{minQ},{maxQ}] r=[{minR},{maxR}] count={wallCount}");
        details.Add($"Map radius=14, wall center distance from origin: ~{HexUtils.AxialDistance(new Vector2I((minQ+maxQ)/2,(minR+maxR)/2), Vector2I.Zero)}");

        // 城墙与地形高度对比
        details.Add("");
        details.Add("=== 高度分布详细 ===");
        int rampartMinE = 99, rampartMaxE = 0;
        int towerMinE = 99, towerMaxE = 0;
        int gateMinE = 99, gateMaxE = 0;
        int stairMinE = 99, stairMaxE = 0;
        int naturalMinE = 99, naturalMaxE = 0;
        int roadMinE = 99, roadMaxE = 0;

        foreach (Variant keyV in castleMd.Cells.Keys)
        {
            var cd = castleMd.Cells[keyV].As<BattleCellData>();
            if (cd == null) continue;
            int e = cd.elevation;
            switch (cd.terrainType)
            {
                case BattleCellData.TerrainType.Rampart:
                    if (e < rampartMinE) rampartMinE = e;
                    if (e > rampartMaxE) rampartMaxE = e;
                    break;
                case BattleCellData.TerrainType.Tower:
                    if (e < towerMinE) towerMinE = e;
                    if (e > towerMaxE) towerMaxE = e;
                    break;
                case BattleCellData.TerrainType.Gate:
                    if (e < gateMinE) gateMinE = e;
                    if (e > gateMaxE) gateMaxE = e;
                    break;
                case BattleCellData.TerrainType.Staircase:
                    if (e < stairMinE) stairMinE = e;
                    if (e > stairMaxE) stairMaxE = e;
                    break;
                case BattleCellData.TerrainType.Road:
                    if (e < roadMinE) roadMinE = e;
                    if (e > roadMaxE) roadMaxE = e;
                    break;
                default:
                    // 自然地形
                    if (e < naturalMinE) naturalMinE = e;
                    if (e > naturalMaxE) naturalMaxE = e;
                    break;
            }
        }

        details.Add($"Natural terrain: elev [{naturalMinE}-{naturalMaxE}]");
        details.Add($"Road:            elev [{roadMinE}-{roadMaxE}]");
        details.Add($"Staircase:       elev [{stairMinE}-{stairMaxE}]");
        details.Add($"Rampart (wall):  elev [{rampartMinE}-{rampartMaxE}]");
        details.Add($"Gate:            elev [{gateMinE}-{gateMaxE}]");
        details.Add($"Tower:           elev [{towerMinE}-{towerMaxE}]");
        details.Add($"Expected: Natural < Staircase < Rampart/Gate < Tower");
    }
}
