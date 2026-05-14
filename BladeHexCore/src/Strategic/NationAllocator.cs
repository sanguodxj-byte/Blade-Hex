// NationAllocator.cs
// 国家版图分配器 — Voronoi 式同步生长
// 每个国家从偏好生态区中心开始，同时向外扩展，先到先得
// 保证每个国家的领土天然连续，无内部空洞
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 国家版图分配器 — Voronoi 式同步生长
/// </summary>
public class NationAllocator
{
    /// <summary>
    /// 为每个国家分配领土 — Voronoi 式同步生长
    /// 每个国家从偏好生态区的中心开始，同时向外扩展，先到先得。
    /// 保证每个国家的领土天然连续。
    /// </summary>
    public Dictionary<string, NationTerritory> AllocateTerritories(
        List<BiomeZone> zones,
        List<NationConfig> nations,
        int seed)
    {
        var rng = new Random(seed ^ 0x4E4154);
        var result = new Dictionary<string, NationTerritory>();

        // 收集所有陆地 tile 坐标
        var allLandTiles = new HashSet<Vector2I>();
        foreach (var zone in zones)
            foreach (var coord in zone.TileCoords)
                allLandTiles.Add(coord);

        if (allLandTiles.Count == 0)
        {
            GD.PrintErr("[NationAllocator] 无陆地 tile");
            return result;
        }

        var tileOwner = new Dictionary<Vector2I, string>();
        var growthQueues = new Dictionary<string, Queue<Vector2I>>();
        var targetSizes = new Dictionary<string, int>();

        // 按优先级排序
        var sorted = nations
            .OrderByDescending(n => n.IsMajorNation ? 1 : 0)
            .ThenByDescending(n => n.PopulationScale)
            .ToList();

        // 为每个国家选种子点
        foreach (var nation in sorted)
        {
            Vector2I? seedPoint = FindSeedPoint(nation, zones, tileOwner, rng);
            if (seedPoint == null)
            {
                GD.PrintErr($"[NationAllocator] 无法为 {nation.DisplayName} 找到种子点");
                continue;
            }

            var territory = new NationTerritory { NationId = nation.Id };
            territory.AllTiles.Add(seedPoint.Value);
            tileOwner[seedPoint.Value] = nation.Id;
            result[nation.Id] = territory;

            var queue = new Queue<Vector2I>();
            for (int dir = 0; dir < 6; dir++)
            {
                var nb = HexOverworldTile.GetNeighbor(seedPoint.Value.X, seedPoint.Value.Y, dir);
                if (allLandTiles.Contains(nb) && !tileOwner.ContainsKey(nb))
                    queue.Enqueue(nb);
            }
            growthQueues[nation.Id] = queue;

            // 目标面积
            float totalPop = sorted.Sum(n => n.PopulationScale);
            float share = nation.PopulationScale / totalPop;
            targetSizes[nation.Id] = (int)(allLandTiles.Count * share * 0.7f);
        }

        // === 同步生长（Phase 1: 按配额） ===
        bool anyGrew = true;
        while (anyGrew)
        {
            anyGrew = false;
            foreach (var nation in sorted)
            {
                if (!result.ContainsKey(nation.Id)) continue;
                var territory = result[nation.Id];
                if (territory.AllTiles.Count >= targetSizes.GetValueOrDefault(nation.Id, 1000)) continue;
                if (!growthQueues.TryGetValue(nation.Id, out var queue) || queue.Count == 0) continue;

                int batchSize = queue.Count;
                for (int i = 0; i < batchSize && queue.Count > 0; i++)
                {
                    var candidate = queue.Dequeue();
                    if (tileOwner.ContainsKey(candidate)) continue;
                    if (!allLandTiles.Contains(candidate)) continue;

                    territory.AllTiles.Add(candidate);
                    tileOwner[candidate] = nation.Id;
                    anyGrew = true;

                    for (int dir = 0; dir < 6; dir++)
                    {
                        var nb = HexOverworldTile.GetNeighbor(candidate.X, candidate.Y, dir);
                        if (!tileOwner.ContainsKey(nb) && allLandTiles.Contains(nb))
                            queue.Enqueue(nb);
                    }
                }
            }
        }

        // === 同步生长（Phase 2: 无配额限制，瓜分剩余陆地） ===
        // 解除面积上限，让所有国家继续扩张直到所有可达陆地被分配
        anyGrew = true;
        while (anyGrew)
        {
            anyGrew = false;
            foreach (var nation in sorted)
            {
                if (!result.ContainsKey(nation.Id)) continue;
                var territory = result[nation.Id];
                if (!growthQueues.TryGetValue(nation.Id, out var queue) || queue.Count == 0) continue;

                int batchSize = queue.Count;
                for (int i = 0; i < batchSize && queue.Count > 0; i++)
                {
                    var candidate = queue.Dequeue();
                    if (tileOwner.ContainsKey(candidate)) continue;
                    if (!allLandTiles.Contains(candidate)) continue;

                    territory.AllTiles.Add(candidate);
                    tileOwner[candidate] = nation.Id;
                    anyGrew = true;

                    for (int dir = 0; dir < 6; dir++)
                    {
                        var nb = HexOverworldTile.GetNeighbor(candidate.X, candidate.Y, dir);
                        if (!tileOwner.ContainsKey(nb) && allLandTiles.Contains(nb))
                            queue.Enqueue(nb);
                    }
                }
            }
        }

        // 设置 CoreZone
        foreach (var (nationId, territory) in result)
        {
            if (territory.AllTiles.Count > 0)
            {
                long sumQ = 0, sumR = 0;
                foreach (var t in territory.AllTiles) { sumQ += t.X; sumR += t.Y; }
                var centroid = new Vector2I((int)(sumQ / territory.AllTiles.Count), (int)(sumR / territory.AllTiles.Count));
                territory.CoreZone = new BiomeZone { Centroid = centroid };
                territory.CoreZone.TileCoords = territory.AllTiles;
            }
        }

        // 后处理：填充被领土边界完全包围的内部格（山脉/水域/任何无主格）
        FillEnclosedTiles(result, tileOwner);

        GD.Print($"[NationAllocator] Voronoi 分配完成: {result.Count}/{nations.Count} 个国家, 总领土 {tileOwner.Count}/{allLandTiles.Count} 陆地格");
        return result;
    }

    private static Vector2I? FindSeedPoint(
        NationConfig nation, List<BiomeZone> zones,
        Dictionary<Vector2I, string> tileOwner, Random rng)
    {
        // 找偏好生态类型的最大未占用区域
        BiomeZone? bestZone = null;
        int bestSize = 0;

        foreach (var zone in zones)
        {
            if (!nation.PreferredBiomes.Contains(zone.DominantBiome)) continue;
            if (zone.TileCount <= bestSize) continue;
            if (tileOwner.ContainsKey(zone.Centroid)) continue;
            bestZone = zone;
            bestSize = zone.TileCount;
        }

        if (bestZone != null) return bestZone.Centroid;

        // Fallback：任何未占用的大区域
        foreach (var zone in zones.OrderByDescending(z => z.TileCount))
        {
            if (!tileOwner.ContainsKey(zone.Centroid))
                return zone.Centroid;
        }

        return null;
    }

    /// <summary>
    /// 填充被领土完全包围的内部格（不区分陆地/非陆地）。
    /// 算法：从地图边缘 flood-fill 找到所有"外部"无主格，
    /// 剩余无主格即为被某国包围的内部格，逐层从边缘向内分配给相邻国家。
    /// </summary>
    private static void FillEnclosedTiles(
        Dictionary<string, NationTerritory> territories,
        Dictionary<Vector2I, string> tileOwner)
    {
        if (territories.Count == 0) return;

        // 1. 确定地图边界（从已分配的所有 tile 坐标推算）
        int minQ = int.MaxValue, maxQ = int.MinValue;
        int minR = int.MaxValue, maxR = int.MinValue;
        foreach (var coord in tileOwner.Keys)
        {
            if (coord.X < minQ) minQ = coord.X;
            if (coord.X > maxQ) maxQ = coord.X;
            if (coord.Y < minR) minR = coord.Y;
            if (coord.Y > maxR) maxR = coord.Y;
        }
        // 向外扩展 1 格确保边缘 flood-fill 能覆盖
        minQ -= 1; maxQ += 1; minR -= 1; maxR += 1;

        // 2. 从边界格开始 flood-fill，标记所有可达外部的无主格
        var exterior = new HashSet<Vector2I>();
        var floodQueue = new Queue<Vector2I>();

        for (int q = minQ; q <= maxQ; q++)
        {
            EnqueueIfUnowned(new Vector2I(q, minR), tileOwner, exterior, floodQueue);
            EnqueueIfUnowned(new Vector2I(q, maxR), tileOwner, exterior, floodQueue);
        }
        for (int r = minR; r <= maxR; r++)
        {
            EnqueueIfUnowned(new Vector2I(minQ, r), tileOwner, exterior, floodQueue);
            EnqueueIfUnowned(new Vector2I(maxQ, r), tileOwner, exterior, floodQueue);
        }

        while (floodQueue.Count > 0)
        {
            var current = floodQueue.Dequeue();
            for (int dir = 0; dir < 6; dir++)
            {
                var nb = HexOverworldTile.GetNeighbor(current.X, current.Y, dir);
                if (nb.X < minQ || nb.X > maxQ || nb.Y < minR || nb.Y > maxR) continue;
                if (tileOwner.ContainsKey(nb)) continue;
                if (exterior.Contains(nb)) continue;
                exterior.Add(nb);
                floodQueue.Enqueue(nb);
            }
        }

        // 3. 收集所有内部无主格（在边界范围内、不在 exterior、不在 tileOwner）
        var interiorUnowned = new HashSet<Vector2I>();
        for (int q = minQ; q <= maxQ; q++)
        {
            for (int r = minR; r <= maxR; r++)
            {
                var coord = new Vector2I(q, r);
                if (tileOwner.ContainsKey(coord)) continue;
                if (exterior.Contains(coord)) continue;
                interiorUnowned.Add(coord);
            }
        }

        if (interiorUnowned.Count == 0) return;

        // 4. 逐层从边缘向内分配：每轮找出与已分配格相邻的内部无主格，归入邻居最多的国家
        int filled = 0;
        while (interiorUnowned.Count > 0)
        {
            var toAssign = new Dictionary<Vector2I, string>();

            foreach (var coord in interiorUnowned)
            {
                // 检查是否与已分配格相邻
                var counts = new Dictionary<string, int>();
                for (int dir = 0; dir < 6; dir++)
                {
                    var nb = HexOverworldTile.GetNeighbor(coord.X, coord.Y, dir);
                    if (tileOwner.TryGetValue(nb, out var owner))
                    {
                        counts.TryGetValue(owner, out int c);
                        counts[owner] = c + 1;
                    }
                }

                if (counts.Count == 0) continue; // 本轮还没有已分配邻居，等下一轮

                // 分配给邻居数最多的国家
                string best = "";
                int bestCount = 0;
                foreach (var (k, v) in counts)
                {
                    if (v > bestCount) { best = k; bestCount = v; }
                }
                if (!string.IsNullOrEmpty(best))
                    toAssign[coord] = best;
            }

            if (toAssign.Count == 0) break; // 防止死循环（理论上不会发生）

            foreach (var (coord, nationId) in toAssign)
            {
                interiorUnowned.Remove(coord);
                if (!territories.ContainsKey(nationId)) continue;
                territories[nationId].AllTiles.Add(coord);
                tileOwner[coord] = nationId;
                filled++;
            }
        }

        if (filled > 0)
            GD.Print($"[NationAllocator] 填充内部包围格: {filled}");
    }

    private static void EnqueueIfUnowned(Vector2I coord,
        Dictionary<Vector2I, string> tileOwner,
        HashSet<Vector2I> exterior,
        Queue<Vector2I> queue)
    {
        if (tileOwner.ContainsKey(coord)) return;
        if (exterior.Contains(coord)) return;
        exterior.Add(coord);
        queue.Enqueue(coord);
    }
}

/// <summary>
/// 国家领土
/// </summary>
public class NationTerritory
{
    public string NationId { get; set; } = "";
    public BiomeZone CoreZone { get; set; } = new();
    public List<BiomeZone> Zones { get; set; } = new();
    public HashSet<Vector2I> AllTiles { get; set; } = new();
    public int TotalTiles => AllTiles.Count;
}
