// HexNavMeshGenerator.cs
// 从六边形网格生成 NavigationPolygon — 用于 NavigationServer2D
// 策略：大矩形外边界 + 不可通行 hex 作为"洞"
// 这样只需要处理不可通行 hex（数量远少于可通行 hex），性能可控
using Godot;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.View.Map;

public static class HexNavMeshGenerator
{
	/// <summary>
	/// 从六边形网格生成 NavigationPolygon
	/// 方案：大矩形覆盖整个地图 + 不可通行 hex 作为洞
	/// </summary>
	public static NavigationPolygon Generate(HexOverworldGrid grid, float padding = 500f)
	{
		var navPolygon = new NavigationPolygon();

		// 1. 计算地图像素边界
		float minX = float.MaxValue, minY = float.MaxValue;
		float maxX = float.MinValue, maxY = float.MinValue;
		foreach (var tile in grid.Tiles.Values)
		{
			minX = Mathf.Min(minX, tile.PixelPos.X);
			minY = Mathf.Min(minY, tile.PixelPos.Y);
			maxX = Mathf.Max(maxX, tile.PixelPos.X);
			maxY = Mathf.Max(maxY, tile.PixelPos.Y);
		}
		minX -= padding;
		minY -= padding;
		maxX += padding;
		maxY += padding;

		// 2. 添加外边界（逆时针 = 可行走区域）
		var outerBoundary = new Vector2[]
		{
			new(minX, minY),
			new(maxX, minY),
			new(maxX, maxY),
			new(minX, maxY),
		};
		navPolygon.AddOutline(outerBoundary);

		// 3. 收集不可通行 hex 的连通区域，合并为洞
		// 简化方案：每个不可通行 hex 单独作为一个洞（六边形）
		// 但如果不可通行 hex 太多（>5000），改为只处理边界 hex
		var impassableTiles = new List<HexOverworldTile>();
		foreach (var tile in grid.Tiles.Values)
		{
			if (!tile.IsPassable)
				impassableTiles.Add(tile);
		}

		GD.Print($"[NavMesh] 不可通行 hex: {impassableTiles.Count}, 总 hex: {grid.Tiles.Count}");

		// 如果不可通行 hex 太多，只处理边界（与可通行 hex 相邻的不可通行 hex）
		List<HexOverworldTile> holeTiles;
		if (impassableTiles.Count > 3000)
		{
			holeTiles = FilterBorderImpassable(grid, impassableTiles);
			GD.Print($"[NavMesh] 过滤后边界不可通行 hex: {holeTiles.Count}");
		}
		else
		{
			holeTiles = impassableTiles;
		}

		// 4. 为每个不可通行 hex 添加洞（顺时针 = 不可行走区域）
		float hexSize = HexOverworldTile.HexSize * 0.95f; // 略小于实际 hex，避免路径贴边
		int holeCount = 0;
		foreach (var tile in holeTiles)
		{
			var hole = GetHexVerticesCW(tile.PixelPos, hexSize);
			navPolygon.AddOutline(hole);
			holeCount++;
		}

		// 5. 让 Godot 自动三角化
		navPolygon.MakePolygonsFromOutlines();

		GD.Print($"[NavMesh] 生成完成: {holeCount} 个洞, 边界 {maxX - minX:.0f}×{maxY - minY:.0f}px");
		return navPolygon;
	}

	/// <summary>
	/// 分批生成版本 — 每帧处理一批不可通行 hex
	/// </summary>
	public static bool GenerateBatched(HexOverworldGrid grid, NavMeshGenerationState state, int batchSize = 200)
	{
		if (state.IsComplete) return true;

		// 初始化阶段
		if (state.NavPolygon == null)
		{
			state.NavPolygon = new NavigationPolygon();

			// 计算边界
			float minX = float.MaxValue, minY = float.MaxValue;
			float maxX = float.MinValue, maxY = float.MinValue;
			foreach (var tile in grid.Tiles.Values)
			{
				minX = Mathf.Min(minX, tile.PixelPos.X);
				minY = Mathf.Min(minY, tile.PixelPos.Y);
				maxX = Mathf.Max(maxX, tile.PixelPos.X);
				maxY = Mathf.Max(maxY, tile.PixelPos.Y);
			}
			float padding = 500f;

			// 外边界
			var outer = new Vector2[]
			{
				new(minX - padding, minY - padding),
				new(maxX + padding, minY - padding),
				new(maxX + padding, maxY + padding),
				new(minX - padding, maxY + padding),
			};
			state.NavPolygon.AddOutline(outer);

			// 收集不可通行 hex
			state.ImpassableList = new List<HexOverworldTile>();
			foreach (var tile in grid.Tiles.Values)
			{
				if (!tile.IsPassable)
					state.ImpassableList.Add(tile);
			}

			// 如果太多，过滤为边界
			if (state.ImpassableList.Count > 3000)
				state.ImpassableList = FilterBorderImpassable(grid, state.ImpassableList);

			state.ProcessedCount = 0;
			GD.Print($"[NavMesh] 分批开始: {state.ImpassableList.Count} 个洞待处理");
		}

		// 处理一批
		float hexSize = HexOverworldTile.HexSize * 0.95f;
		int end = Mathf.Min(state.ProcessedCount + batchSize, state.ImpassableList!.Count);
		for (int i = state.ProcessedCount; i < end; i++)
		{
			var tile = state.ImpassableList[i];
			var hole = GetHexVerticesCW(tile.PixelPos, hexSize);
			state.NavPolygon.AddOutline(hole);
		}
		state.ProcessedCount = end;

		// 完成
		if (state.ProcessedCount >= state.ImpassableList.Count)
		{
			// 如果没有洞，直接创建简单矩形 navmesh
			if (state.ProcessedCount == 0)
			{
				var outer = state.NavPolygon.GetOutline(0);
				state.NavPolygon.Vertices = outer;
				state.NavPolygon.AddPolygon(new int[] { 0, 1, 2 });
				state.NavPolygon.AddPolygon(new int[] { 0, 2, 3 });
			}
			else
			{
				state.NavPolygon.MakePolygonsFromOutlines();
			}
			state.IsComplete = true;
			GD.Print($"[NavMesh] 分批完成: {state.ProcessedCount} 个洞");
		}

		return state.IsComplete;
	}

	/// <summary>过滤：只保留与可通行 hex 相邻的不可通行 hex（边界）</summary>
	private static List<HexOverworldTile> FilterBorderImpassable(
		HexOverworldGrid grid, List<HexOverworldTile> impassable)
	{
		var result = new List<HexOverworldTile>();
		foreach (var tile in impassable)
		{
			var neighbors = grid.GetNeighbors(tile.Coord.X, tile.Coord.Y);
			foreach (var n in neighbors)
			{
				if (n.IsPassable)
				{
					result.Add(tile);
					break;
				}
			}
		}
		return result;
	}

	/// <summary>获取 hex 的 6 个顶点（顺时针，用于洞）</summary>
	private static Vector2[] GetHexVerticesCW(Vector2 center, float size)
	{
		var verts = new Vector2[6];
		// 顺时针：0°, 300°, 240°, 180°, 120°, 60°
		for (int i = 0; i < 6; i++)
		{
			float angle = Mathf.DegToRad(-60f * i); // 负角度 = 顺时针
			verts[i] = new Vector2(
				center.X + size * Mathf.Cos(angle),
				center.Y + size * Mathf.Sin(angle)
			);
		}
		return verts;
	}

	/// <summary>获取 hex 的 6 个顶点（逆时针）</summary>
	public static Vector2[] GetHexVertices(Vector2 center, float scale = 1.0f)
	{
		float size = HexOverworldTile.HexSize * scale;
		var verts = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float angle = Mathf.DegToRad(60f * i);
			verts[i] = new Vector2(
				center.X + size * Mathf.Cos(angle),
				center.Y + size * Mathf.Sin(angle)
			);
		}
		return verts;
	}
}

/// <summary>分批生成状态</summary>
public class NavMeshGenerationState
{
	public NavigationPolygon? NavPolygon { get; set; }
	public List<Vector2> Vertices { get; set; } = new();
	public Dictionary<Vector2I, int> VertexIndexMap { get; set; } = new();
	public Dictionary<Vector2I, HexOverworldTile>.Enumerator TileEnumerator { get; set; }
	public List<HexOverworldTile>? ImpassableList { get; set; }
	public int ProcessedCount { get; set; } = 0;
	public bool IsComplete { get; set; } = false;
}
