// OverworldEntityNavigator.cs
// Core 层 AI 实体导航 Adapter：统一 chunk/legacy 寻路选择与实体路径写入。
using Godot;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// AI 实体导航 Adapter。
/// 
/// 策略:
///   - 休眠实体按日直线推进，不进入 A*。
///   - chunk 导航存在时优先使用 chunk 路径；chunk A* 自己处理“目标未加载时走到边界”。
///   - 仅未配置 chunk 导航时才使用 legacy HexAStar，避免 chunk 模式下旧全图路径泄漏。
/// </summary>
public sealed class OverworldEntityNavigator
{
    private const float DormantDailyDistance = 600.0f;

    private HexOverworldAStar? _hexAstar;
    private ChunkManager? _chunkManager;
    private ChunkAStar? _chunkAstar;

    public void SetHexNavigation(HexOverworldGrid grid, HexOverworldAStar astar)
    {
        astar.Grid ??= grid;
        _hexAstar = astar;
    }

    public void SetChunkNavigation(ChunkManager manager, ChunkAStar astar)
    {
        _chunkManager = manager;
        _chunkAstar = astar;
    }

    /// <summary>
    /// 启动实体移动并写入 Path/TargetPosition/IsMoving。
    /// 返回 false 表示当前导航源无法产生有效移动。
    /// </summary>
    public bool StartMoveTo(OverworldEntity entity, Vector2 target)
    {
        if (entity.Lod == OverworldEntity.EntityLod.Hibernated)
        {
            AdvanceDormantEntity(entity, target);
            return true;
        }

        var path = FindPath(entity.Position, target);
        if (path.Length == 0)
        {
            Stop(entity);
            return false;
        }

        ApplyPath(entity, target, path);
        return true;
    }

    private Vector2[] FindPath(Vector2 from, Vector2 to)
    {
        if (_chunkManager != null && _chunkAstar != null)
            return _chunkAstar.FindPathPixels(from, to, _chunkManager);

        if (_hexAstar != null)
            return _hexAstar.FindPathPixels(from, to);

        return [];
    }

    private static void AdvanceDormantEntity(OverworldEntity entity, Vector2 target)
    {
        float distance = entity.Position.DistanceTo(target);

        if (distance <= DormantDailyDistance)
        {
            entity.Position = target;
            entity.IsMoving = false;
            entity.Path.Clear();
            return;
        }

        Vector2 direction = (target - entity.Position).Normalized();
        entity.Position += direction * DormantDailyDistance;
        entity.IsMoving = true;
        entity.TargetPosition = target;
        entity.Path.Clear();
    }

    private static void ApplyPath(OverworldEntity entity, Vector2 target, Vector2[] path)
    {
        entity.Path.Clear();
        foreach (var point in path)
            entity.Path.Add(point);

        entity.IsMoving = true;
        entity.TargetPosition = target;
    }

    private static void Stop(OverworldEntity entity)
    {
        entity.IsMoving = false;
        entity.Path.Clear();
    }
}
