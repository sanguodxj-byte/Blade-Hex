// MountainDepthStage.cs
// 世界生成阶段 2.5：山脉深度计算 — 山脉内部越深处高程越高。
//
// 算法：
//   1. 遍历所有 Mountain/MountainSnow/Hills/Rocky hex
//   2. 对每个山地 hex，BFS 计算到最近非山地 hex 的距离（hex 步数）
//   3. 将距离映射为额外高程加成，写入 tile.Elevation
//
// 效果：山脉边缘低、中心高，形成自然的山脊/山峰。
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Map;

namespace BladeHex.Strategic.WorldGen.Stages;

/// <summary>
/// 阶段 2.5：山脉深度 — 让山脉内部越深处高程越高，模拟真实山脊。
/// </summary>
public sealed class MountainDepthStage : IWorldStage
{
    public string Name => "山脉深度";
    public float ProgressWeight => 0.5f;

    /// <summary>每层深度增加的高程值</summary>
    private const float ElevPerDepth = 0.06f;

    /// <summary>最大深度（防止极端值）</summary>
    private const int MaxDepth = 8;

    public void Execute(WorldBuildContext ctx)
    {
        // 收集所有 tile 到平面字典（方便邻居查询）
        var allTiles = new Dictionary<Vector2I, HexOverworldTile>();
        foreach (var chunk in ctx.Chunks.Values)
        {
            foreach (var tile in chunk.Tiles.Values)
                allTiles[tile.Coord] = tile;
        }

        // 找出所有山地 hex 和边界山地 hex（至少有一个非山地邻居）
        var mountainTiles = new HashSet<Vector2I>();
        var edgeMountains = new Queue<Vector2I>();
        var depthMap = new Dictionary<Vector2I, int>();

        foreach (var (coord, tile) in allTiles)
        {
            if (!IsMountainous(tile.Terrain)) continue;
            mountainTiles.Add(coord);

            // 检查是否是边界（有非山地邻居）
            bool isEdge = false;
            foreach (var nCoord in HexNeighbors(coord))
            {
                if (!allTiles.TryGetValue(nCoord, out var neighbor) || !IsMountainous(neighbor.Terrain))
                {
                    isEdge = true;
                    break;
                }
            }

            if (isEdge)
            {
                edgeMountains.Enqueue(coord);
                depthMap[coord] = 0;
            }
        }

        // BFS 从边界向内扩展，计算每个山地 hex 的深度
        while (edgeMountains.Count > 0)
        {
            var current = edgeMountains.Dequeue();
            int currentDepth = depthMap[current];

            foreach (var nCoord in HexNeighbors(current))
            {
                if (!mountainTiles.Contains(nCoord)) continue;
                if (depthMap.ContainsKey(nCoord)) continue;

                int newDepth = currentDepth + 1;
                if (newDepth > MaxDepth) newDepth = MaxDepth;
                depthMap[nCoord] = newDepth;
                edgeMountains.Enqueue(nCoord);
            }
        }

        // 将深度写入 tile.Elevation
        int modified = 0;
        foreach (var (coord, depth) in depthMap)
        {
            if (depth <= 0) continue;
            if (!allTiles.TryGetValue(coord, out var tile)) continue;

            float bonus = depth * ElevPerDepth;
            tile.Elevation = Mathf.Clamp(tile.Elevation + bonus, 0.0f, 1.0f);
            modified++;
        }

        GD.Print($"[MountainDepthStage] 处理 {mountainTiles.Count} 个山地 hex, " +
                 $"边界 {depthMap.Count - modified} 个, 内部加深 {modified} 个, " +
                 $"最大深度 {(depthMap.Count > 0 ? depthMap.Values.Max() : 0)}");
    }

    /// <summary>判断地形是否属于"山地系"</summary>
    private static bool IsMountainous(HexOverworldTile.TerrainType terrain)
    {
        return terrain == HexOverworldTile.TerrainType.Mountain
            || terrain == HexOverworldTile.TerrainType.MountainSnow
            || terrain == HexOverworldTile.TerrainType.Hills
            || terrain == HexOverworldTile.TerrainType.Rocky;
    }

    /// <summary>Axial 坐标的 6 个邻居</summary>
    private static IEnumerable<Vector2I> HexNeighbors(Vector2I coord)
    {
        yield return new Vector2I(coord.X + 1, coord.Y);
        yield return new Vector2I(coord.X - 1, coord.Y);
        yield return new Vector2I(coord.X, coord.Y + 1);
        yield return new Vector2I(coord.X, coord.Y - 1);
        yield return new Vector2I(coord.X + 1, coord.Y - 1);
        yield return new Vector2I(coord.X - 1, coord.Y + 1);
    }
}
