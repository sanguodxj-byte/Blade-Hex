using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.View.Map;

namespace BladeHex.Map;

/// <summary>
/// 管理六边形网格的生成、查询和寻路逻辑 (HD-2D 3D版本)
/// </summary>
[GlobalClass]
public partial class HexGrid : Node3D
{
    [Signal] public delegate void CellClickedEventHandler(HexCell cell);

    // 存储单元格数据: { Vector2I(q, r): HexCell }
    public Godot.Collections.Dictionary<Vector2I, HexCell> Cells { get; set; } = new();

    /// <summary>合批渲染管理器 — 由 _Ready 创建并注册到全局单例</summary>
    private HexCellMultiMeshBatcher? _batcher;

    public override void _Ready()
    {
        // 创建合批器作为子节点（一个 HexGrid 一个 Batcher）
        _batcher = new HexCellMultiMeshBatcher();
        AddChild(_batcher);
        HexCellMultiMeshBatcher.Instance = _batcher;
    }

    public void CreateCell(int q, int r, int elevation = 1, int cover = 0)
    {
        var gridPos = new Vector2I(q, r);
        var cellPos = HexUtils.AxialToWorld3D(q, r, elevation);

        var cell = new HexCell();
        cell.Name = $"HexCell_{q}_{r}";
        cell.Position = cellPos;
        cell.GridPos = gridPos;
        cell.Elevation = elevation;
        cell.CoverType = cover;
        cell.Batcher = _batcher; // 注入合批器引用
        cell.CellSingleClicked += (c) => EmitSignal(SignalName.CellClicked, c);
        AddChild(cell);

        Cells[gridPos] = cell;
    }

    /// <summary>从 BattleMapData 加载地图（由 BattleMapGenerator 生成的数据）</summary>
    public void LoadFromMapData(BattleMapGenerator.BattleMapData mapData)
    {
        // 确保 Batcher 存在（防御性：场景未触发 _Ready 时）
        if (_batcher == null)
        {
            _batcher = new HexCellMultiMeshBatcher();
            AddChild(_batcher);
            HexCellMultiMeshBatcher.Instance = _batcher;
        }

        // 清空现有格子
        foreach (var cell in Cells.Values)
        {
            if (GodotObject.IsInstanceValid(cell))
                cell.QueueFree();
        }
        Cells.Clear();

        foreach (Variant keyVariant in mapData.cells.Keys)
        {
            Vector2I key = keyVariant.AsVector2I();
            var cellData = mapData.cells[keyVariant].As<BattleCellData>();
            if (cellData == null) continue;

            int q = key.X;
            int r = key.Y;
            int elev = cellData.elevation;
            int cover = cellData.coverLevel;

            var cellPos = HexUtils.AxialToWorld3D(q, r, elev);

            var cell = new HexCell();
            cell.Name = $"HexCell_{q}_{r}";
            cell.Position = cellPos;
            cell.GridPos = key;
            cell.Elevation = elev;
            cell.CoverType = cover;
            cell.Data = cellData;
            cell.Batcher = _batcher; // 注入合批器引用
            cell.CellSingleClicked += (c) => EmitSignal(SignalName.CellClicked, c);
            AddChild(cell);

            Cells[key] = cell;
        }
    }

    /// <summary>获取指定坐标的单元格</summary>
    public HexCell? GetCell(int q, int r)
    {
        var pos = new Vector2I(q, r);
        return Cells.ContainsKey(pos) ? Cells[pos] : null;
    }

    /// <summary>获取所有单元格</summary>
    public IEnumerable<HexCell> GetCells() => Cells.Values;

    /// <summary>获取移动力范围内的所有坐标 (Dijkstra 算法)</summary>
    public List<Vector2I> GetCellsInRange(int startQ, int startR, float movePoints)
    {
        var startPos = new Vector2I(startQ, startR);
        var costSoFar = new Dictionary<Vector2I, float>();
        costSoFar[startPos] = 0.0f;
        
        var frontier = new PriorityQueue<Vector2I, float>();
        frontier.Enqueue(startPos, 0.0f);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            if (!Cells.ContainsKey(current)) continue;
            var currentCell = Cells[current];

            for (int dir = 0; dir < 6; dir++)
            {
                var nextPos = HexUtils.GetNeighbor(current.X, current.Y, dir);
                if (!Cells.ContainsKey(nextPos)) continue;

                var nextCell = Cells[nextPos];
                int elevDiff = nextCell.Elevation - currentCell.Elevation;

                // 规则：高度差 >= 2 视为不可通行，上移最多一层
                if (Math.Abs(elevDiff) >= 2 || elevDiff > 1) continue;

                float moveCost = nextCell.Data != null ? (float)nextCell.Data.moveCost : 1.0f;
                if (elevDiff > 0) moveCost += 3.0f; // 低向高位移额外花费 3 点 AP

                float newCost = costSoFar[current] + moveCost;
                if (newCost <= movePoints)
                {
                    if (!costSoFar.ContainsKey(nextPos) || newCost < costSoFar[nextPos])
                    {
                        costSoFar[nextPos] = newCost;
                        frontier.Enqueue(nextPos, newCost);
                    }
                }
            }
        }
        
        var result = costSoFar.Keys.ToList();
        result.Remove(startPos); // 不包含起点
        return result;
    }

    /// <summary>A* 寻路算法，计算两个六边形之间的最短可通行路径</summary>
    public List<Vector2I> FindPath(Vector2I startPos, Vector2I targetPos)
    {
        if (!Cells.ContainsKey(startPos) || !Cells.ContainsKey(targetPos))
            return new List<Vector2I>();

        var targetCell = Cells[targetPos];
        // 如果目标已被占据，直接返回空路径
        if (targetCell.Occupant != null)
            return new List<Vector2I>();

        var frontier = new PriorityQueue<Vector2I, float>();
        frontier.Enqueue(startPos, 0.0f);

        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var costSoFar = new Dictionary<Vector2I, float>();

        cameFrom[startPos] = startPos;
        costSoFar[startPos] = 0.0f;

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == targetPos) break;

            if (!Cells.ContainsKey(current)) continue;
            var currentCell = Cells[current];

            for (int dir = 0; dir < 6; dir++)
            {
                var nextPos = HexUtils.GetNeighbor(current.X, current.Y, dir);
                if (!Cells.ContainsKey(nextPos)) continue;

                var nextCell = Cells[nextPos];
                int elevDiff = nextCell.Elevation - currentCell.Elevation;

                // 规则1：高度差 >= 2 视为不可通行
                if (Math.Abs(elevDiff) >= 2)
                    continue;

                // 规则：一次最多只能上移一层
                if (elevDiff > 1)
                    continue;

                // 规则1.5：不可通行地形
                if (nextCell.Data != null && !nextCell.Data.isPassable)
                    continue;

                // 规则2：不能穿过其他单位
                if (nextCell.Occupant != null && nextPos != targetPos)
                    continue;

                float moveCost = nextCell.Data != null ? (float)nextCell.Data.moveCost : 1.0f;
                
                // 规则：低向高位移额外花费 3 点 AP
                if (nextCell.Elevation > currentCell.Elevation)
                    moveCost += 3.0f;

                float newCost = costSoFar[current] + moveCost;

                if (!costSoFar.ContainsKey(nextPos) || newCost < costSoFar[nextPos])
                {
                    costSoFar[nextPos] = newCost;
                    float priority = newCost + HexUtils.Distance(nextPos.X, nextPos.Y, targetPos.X, targetPos.Y);
                    frontier.Enqueue(nextPos, priority);
                    cameFrom[nextPos] = current;
                }
            }
        }

        if (!cameFrom.ContainsKey(targetPos))
            return new List<Vector2I>();

        var path = new List<Vector2I>();
        var curr = targetPos;
        while (curr != startPos)
        {
            path.Add(curr);
            curr = cameFrom[curr];
        }
        path.Reverse();
        return path;
    }

    public float GetPathCost(List<Vector2I> path)
    {
        if (path == null || path.Count <= 1) return 0.0f;

        float totalCost = 0.0f;
        for (int i = 1; i < path.Count; i++)
        {
            var current = path[i - 1];
            var next = path[i];

            if (!Cells.ContainsKey(current) || !Cells.ContainsKey(next)) continue;

            var currentCell = Cells[current];
            var nextCell = Cells[next];

            float moveCost = 1.0f; // 基础成本
            if (nextCell.Data != null) moveCost = nextCell.Data.moveCost;

            // 高程惩罚：低向高额外 +3
            if (nextCell.Elevation > currentCell.Elevation)
                moveCost += 3.0f;

            totalCost += moveCost;
        }
        return totalCost;
    }
}
