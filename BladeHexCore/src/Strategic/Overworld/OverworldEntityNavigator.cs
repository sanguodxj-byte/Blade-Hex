// OverworldEntityNavigator.cs
// Core 层 AI 实体导航 Adapter：统一 chunk/legacy 寻路选择与实体路径写入。
// 日志统一由 OverworldDiagnostics 输出，不再自行限频。
using BladeHex.Map;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// AI 实体导航 Adapter。
/// 
/// 策略:
///   - 休眠实体按日直线推进，不进入 A*。
///   - chunk 导航存在时优先使用 chunk 路径；chunk A* 自己处理"目标未加载时走到边界"。
///   - 仅未配置 chunk 导航时才使用 legacy HexAStar，避免 chunk 模式下旧全图路径泄漏。
/// </summary>
public sealed class OverworldEntityNavigator
{
    private const float DormantDailyDistance = 600.0f;
    private const float CurrentPointEpsilon = 1.0f;
    private const float CaravanRoadPreference = 1.0f;

    private HexOverworldAStar? _hexAstar;
    private ChunkManager? _chunkManager;
    private ChunkAStar? _chunkAstar;
    private readonly ChunkPathFallback _chunkFallback = new();
    private Vector2 _playerPosition = Vector2.Zero;

    // 路径失败日志限频：同一实体-目标组合每 10 次调用才记录一次
    private readonly Dictionary<(string, string), int> _failureLogCounter = new();
    private const int FAILURE_LOG_INTERVAL = 10;

    public void SetHexNavigation(HexOverworldGrid grid, HexOverworldAStar astar)
    {
        astar.Grid ??= grid;
        _hexAstar = astar;
    }

    public void SetChunkNavigation(ChunkManager manager, ChunkAStar astar)
    {
        _chunkManager = manager;
        _chunkAstar = astar;
        _chunkFallback.SetChunkManager(manager);
    }

    public void SetPlayerPosition(Vector2 playerPosition)
    {
        _playerPosition = playerPosition;
    }

    /// <summary>
    /// 启动实体移动并写入 Path/TargetPosition/IsMoving。
    /// 返回 false 表示当前导航源无法产生有效移动。
    /// 路径失败日志按实体限频（每秒最多 1 条），防止 StartMoveTo 每帧重试刷屏。
    /// </summary>
    public bool StartMoveTo(OverworldEntity entity, Vector2 target)
    {
        if (entity.Lod == OverworldEntity.EntityLod.Hibernated)
        {
            AdvanceDormantEntity(entity, target);
            return true;
        }

        var path = FindPath(entity, entity.Position, target);
        if (path.Length == 0)
        {
            if (PreserveTacticalPathOnRefreshFailure(entity))
                return true;

            if (HasNavigationSource() && TryApplyFallbackPath(entity, target))
                return true;

            // 限频：同一实体-目标组合每 FAILURE_LOG_INTERVAL 次才记录一次日志
            var key = (entity.EntityName, $"({target.X:F0},{target.Y:F0})");
            _failureLogCounter.TryGetValue(key, out int count);
            _failureLogCounter[key] = ++count;
            if (count % FAILURE_LOG_INTERVAL == 1)
                OverworldDiagnostics.LogPathFailure(entity, target);

            Stop(entity);
            return false;
        }

        var movementPath = RemoveCurrentPositionPrefix(entity, path);
        if (movementPath.Length == 0)
        {
            if (entity.Position.DistanceTo(target) <= CurrentPointEpsilon)
            {
                Stop(entity);
                entity.TargetPosition = target;
                return true;
            }

            if (PreserveTacticalPathOnRefreshFailure(entity))
                return true;

            if (HasNavigationSource() && TryApplyFallbackPath(entity, target))
                return true;

            Stop(entity);
            return false;
        }

        ApplyPath(entity, target, movementPath);
        return true;
    }

    private bool TryApplyFallbackPath(OverworldEntity entity, Vector2 target)
    {
        var result = _chunkFallback.Resolve(entity, target, _playerPosition);
        Vector2 intermediate;
        if (result.Success)
        {
            intermediate = result.IntermediateTarget;
        }
        else
        {
            intermediate = GetLinearFallbackTarget(entity, target);
        }

        entity.Path.Clear();
        entity.Path.Add(intermediate);
        entity.IsMoving = true;
        entity.TargetPosition = target;

        return true;
    }

    private bool HasNavigationSource()
        => (_chunkManager != null && _chunkAstar != null) || _hexAstar != null;

    private static Vector2 GetLinearFallbackTarget(OverworldEntity entity, Vector2 target)
    {
        float dist = entity.Position.DistanceTo(target);
        if (dist <= 1.0f)
            return target;

        const float stepDistance = 200.0f;
        float step = System.Math.Min(stepDistance, dist);
        return entity.Position + (target - entity.Position).Normalized() * step;
    }

    private Vector2[] FindPath(OverworldEntity entity, Vector2 from, Vector2 to)
    {
        bool preferRoads = ShouldPreferRoads(entity);
        bool ignoreRoadOverlayCost = ShouldIgnoreRoadOverlayCost(entity);

        if (_chunkManager != null && _chunkAstar != null)
        {
            if (!preferRoads && !ignoreRoadOverlayCost)
                return _chunkAstar.FindPathPixels(from, to, _chunkManager);

            float originalPreference = _chunkAstar.RoadPreferenceFactor;
            bool originalIgnoreRoadOverlayCost = _chunkAstar.IgnoreRoadOverlayCostForPath;
            _chunkAstar.InvalidateCache();
            try
            {
                _chunkAstar.RoadPreferenceFactor = preferRoads
                    ? System.Math.Max(originalPreference, CaravanRoadPreference)
                    : 0.0f;
                _chunkAstar.IgnoreRoadOverlayCostForPath = ignoreRoadOverlayCost;
                return _chunkAstar.FindPathPixels(from, to, _chunkManager);
            }
            finally
            {
                _chunkAstar.RoadPreferenceFactor = originalPreference;
                _chunkAstar.IgnoreRoadOverlayCostForPath = originalIgnoreRoadOverlayCost;
                _chunkAstar.InvalidateCache();
            }
        }

        if (_hexAstar != null)
        {
            if (!preferRoads && !ignoreRoadOverlayCost)
                return _hexAstar.FindPathPixels(from, to);

            float originalPreference = _hexAstar.RoadPreference;
            bool originalIgnoreRoadOverlayCost = _hexAstar.IgnoreRoadOverlayCostForPath;
            try
            {
                _hexAstar.RoadPreference = preferRoads
                    ? System.Math.Max(originalPreference, CaravanRoadPreference)
                    : 0.0f;
                _hexAstar.IgnoreRoadOverlayCostForPath = ignoreRoadOverlayCost;
                return _hexAstar.FindPathPixels(from, to);
            }
            finally
            {
                _hexAstar.RoadPreference = originalPreference;
                _hexAstar.IgnoreRoadOverlayCostForPath = originalIgnoreRoadOverlayCost;
            }
        }

        return [];
    }

    private static bool ShouldPreferRoads(OverworldEntity entity)
        => entity.EntityTypeEnum == OverworldEntity.EntityType.Caravan
            && entity.CurrentAIState != OverworldEntity.AIState.Chasing
            && entity.CurrentAIState != OverworldEntity.AIState.Fleeing;

    private static bool ShouldIgnoreRoadOverlayCost(OverworldEntity entity)
        => entity.EntityTypeEnum == OverworldEntity.EntityType.Caravan
            && (entity.CurrentAIState == OverworldEntity.AIState.Chasing
                || entity.CurrentAIState == OverworldEntity.AIState.Fleeing);

    private static bool PreserveTacticalPathOnRefreshFailure(OverworldEntity entity)
    {
        if (entity.Path.Count == 0)
            return false;

        if (entity.CurrentAIState != OverworldEntity.AIState.Chasing &&
            entity.CurrentAIState != OverworldEntity.AIState.Fleeing)
            return false;

        entity.IsMoving = true;
        return true;
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

    private static Vector2[] RemoveCurrentPositionPrefix(OverworldEntity entity, Vector2[] path)
    {
        int firstMovePoint = 0;
        while (firstMovePoint < path.Length &&
               entity.Position.DistanceTo(path[firstMovePoint]) <= CurrentPointEpsilon)
        {
            firstMovePoint++;
        }

        if (firstMovePoint == 0)
            return path;

        if (firstMovePoint >= path.Length)
            return [];

        var result = new Vector2[path.Length - firstMovePoint];
        System.Array.Copy(path, firstMovePoint, result, 0, result.Length);
        return result;
    }

    private static void Stop(OverworldEntity entity)
    {
        entity.IsMoving = false;
        entity.Path.Clear();
    }
}
