// ICombatGridQuery.cs
// 战斗场景格子查询接口 — 只读访问网格状态，不暴露完整 HexGrid。
using System.Collections.Generic;
using Godot;
using BladeHex.Map;

namespace BladeHex.Scenes;

/// <summary>
/// 战斗场景格子查询。提供格子、路径、范围的只读查询。
/// </summary>
public interface ICombatGridQuery
{
	/// <summary>根据网格坐标获取格子。</summary>
	HexCell? GetCell(int q, int r);

	/// <summary>根据网格坐标获取格子。</summary>
	HexCell? GetCell(Vector2I gridPos);

	/// <summary>查找两点间的路径（不含起点）。</summary>
	List<Vector2I>? FindPath(Vector2I from, Vector2I to);

	/// <summary>计算路径的 AP 消耗。</summary>
	float GetPathCost(Vector2I from, List<Vector2I> path);

	/// <summary>获取两点间的六角距离。</summary>
	int GetAxialDistance(Vector2I a, Vector2I b);

	/// <summary>获取指定范围内的所有格子坐标。</summary>
	List<Vector2I> GetCellsInRange(int startQ, int startR, float movePoints);

	/// <summary>检查格子是否可通过。</summary>
	bool IsCellPassable(HexCell cell);

	/// <summary>检查格子是否被占据。</summary>
	bool IsCellOccupied(HexCell cell);
}
