using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 基于二维网格的实体空间索引结构，用于将大地图实体位置过滤从 O(N) 降低到 O(k)。
/// </summary>
public class EntitySpatialIndex
{
    private readonly float _cellSize;
    private readonly Dictionary<long, List<OverworldEntity>> _grid = new();

    public EntitySpatialIndex(float cellSize = 800f)
    {
        _cellSize = cellSize;
    }

    /// <summary>
    /// 将 2D 网格坐标 cellX 与 cellY 合并为一个长整型 Key。
    /// 支持负数单元格，利用位拼接实现。
    /// </summary>
    private long GetKey(Vector2 position)
    {
        int cellX = Mathf.FloorToInt(position.X / _cellSize);
        int cellY = Mathf.FloorToInt(position.Y / _cellSize);
        return ((long)cellX << 32) | (uint)cellY;
    }

    /// <summary>
    /// 清空网格并全量载入实体列表
    /// </summary>
    public void Rebuild(IList<OverworldEntity> entities)
    {
        _grid.Clear();
        foreach (var entity in entities)
        {
            if (entity.IsAlive)
            {
                Insert(entity);
            }
        }
    }

    /// <summary>
    /// 增量更新实体在网格中的位置。若实体移出了旧 cell 则进行迁移。
    /// </summary>
    public void Update(OverworldEntity entity, Vector2 oldPos)
    {
        if (!entity.IsAlive)
        {
            Remove(entity, oldPos);
            return;
        }

        long oldKey = GetKey(oldPos);
        long newKey = GetKey(entity.Position);

        if (oldKey != newKey)
        {
            Remove(entity, oldPos);
            Insert(entity);
        }
    }

    /// <summary>
    /// 公开 Insert API,用于显式插入新实体(避免调用方用 fake oldPos hack)
    /// </summary>
    public void Add(OverworldEntity entity)
    {
        if (entity == null || !entity.IsAlive) return;
        Insert(entity);
    }

    /// <summary>
    /// 插入实体到网格
    /// </summary>
    private void Insert(OverworldEntity entity)
    {
        long key = GetKey(entity.Position);
        if (!_grid.TryGetValue(key, out var list))
        {
            list = new List<OverworldEntity>();
            _grid[key] = list;
        }
        list.Add(entity);
    }

    /// <summary>
    /// 从网格指定位置移除实体
    /// </summary>
    public void Remove(OverworldEntity entity, Vector2 position)
    {
        long key = GetKey(position);
        if (_grid.TryGetValue(key, out var list))
        {
            list.Remove(entity);
            if (list.Count == 0)
            {
                _grid.Remove(key);
            }
        }
    }

    /// <summary>
    /// 查询以 center 为中心、radius 为半径的圆形区域内的实体。
    /// 返回独立的 List 快照,因此调用方在迭代结果时 mutate 索引(Add/Remove/Update)不影响本次结果。
    /// </summary>
    public List<OverworldEntity> QueryRadius(Vector2 center, float radius)
    {
        var result = new List<OverworldEntity>();

        float minX = center.X - radius;
        float maxX = center.X + radius;
        float minY = center.Y - radius;
        float maxY = center.Y + radius;

        int startCellX = Mathf.FloorToInt(minX / _cellSize);
        int endCellX = Mathf.FloorToInt(maxX / _cellSize);
        int startCellY = Mathf.FloorToInt(minY / _cellSize);
        int endCellY = Mathf.FloorToInt(maxY / _cellSize);

        float radiusSq = radius * radius;

        for (int x = startCellX; x <= endCellX; x++)
        {
            for (int y = startCellY; y <= endCellY; y++)
            {
                long key = ((long)x << 32) | (uint)y;
                if (_grid.TryGetValue(key, out var list))
                {
                    int count = list.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var entity = list[i];
                        if (entity.IsAlive && entity.Position.DistanceSquaredTo(center) <= radiusSq)
                        {
                            result.Add(entity);
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 查询在 min 与 max 构成的 AABB 矩形区域内的实体。返回独立 List 快照。
    /// </summary>
    public List<OverworldEntity> QueryRect(Vector2 min, Vector2 max)
    {
        var result = new List<OverworldEntity>();

        int startCellX = Mathf.FloorToInt(min.X / _cellSize);
        int endCellX = Mathf.FloorToInt(max.X / _cellSize);
        int startCellY = Mathf.FloorToInt(min.Y / _cellSize);
        int endCellY = Mathf.FloorToInt(max.Y / _cellSize);

        for (int x = startCellX; x <= endCellX; x++)
        {
            for (int y = startCellY; y <= endCellY; y++)
            {
                long key = ((long)x << 32) | (uint)y;
                if (_grid.TryGetValue(key, out var list))
                {
                    int count = list.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var entity = list[i];
                        if (entity.IsAlive && 
                            entity.Position.X >= min.X && entity.Position.X <= max.X &&
                            entity.Position.Y >= min.Y && entity.Position.Y <= max.Y)
                        {
                            result.Add(entity);
                        }
                    }
                }
            }
        }

        return result;
    }
}
