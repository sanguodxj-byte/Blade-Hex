// ChunkPathFallback.cs
// Chunk 寻路失败降级策略 — 明确 chunk 未加载时 AI 如何移动
//
// 设计目标:
//   - 走到已加载边界
//   - 使用低精度直线推进
//   - 或休眠推进
//   - 对每种失败给出日志原因，而不是只返回空 path
//   - path_failed 日志包含 start、target、chunk loaded 状态、LOD
//   - 玩家附近实体不应因为 chunk 边界原地停住
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>降级策略类型</summary>
public enum ChunkFallbackStrategy
{
    /// <summary>走到已加载 chunk 边界后停止</summary>
    WalkToLoadedBorder,
    /// <summary>低精度直线推进（忽略地形）</summary>
    LinearAdvance,
    /// <summary>休眠推进（一次性计算到达）</summary>
    DormantAdvance,
}

/// <summary>降级结果</summary>
public readonly struct ChunkFallbackResult
{
    public bool Success { get; }
    public ChunkFallbackStrategy Strategy { get; }
    public Vector2 IntermediateTarget { get; }
    public string Reason { get; }

    public ChunkFallbackResult(bool success, ChunkFallbackStrategy strategy, Vector2 intermediateTarget, string reason)
    {
        Success = success;
        Strategy = strategy;
        IntermediateTarget = intermediateTarget;
        Reason = reason;
    }

    public static readonly ChunkFallbackResult Failed = new(false, ChunkFallbackStrategy.WalkToLoadedBorder, Vector2.Zero, "no_fallback_available");
}

/// <summary>
/// Chunk 寻路降级处理器。
///
/// 管线位置:
///   OverworldEntityNavigator.FindPath 返回空 → ChunkPathFallback.Resolve() → 降级移动
///
/// 策略选择:
///   - 玩家附近（< 800px）: WalkToLoadedBorder（保持可见性）
///   - 远距离活跃实体: LinearAdvance（不阻塞推进）
///   - 休眠实体: DormantAdvance（一次性结算）
/// </summary>
public sealed class ChunkPathFallback
{
    private const float PlayerProximityThreshold = 800.0f;
    private const float LinearStepDistance = 200.0f;

    private ChunkManager? _chunkManager;

    public void SetChunkManager(ChunkManager manager)
    {
        _chunkManager = manager;
    }

    /// <summary>
    /// 当主寻路失败时，尝试降级移动。
    /// </summary>
    /// <param name="entity">需要移动的实体</param>
    /// <param name="target">最终目标位置</param>
    /// <param name="playerPos">玩家位置（用于距离判定）</param>
    /// <returns>降级结果，包含中间目标和策略</returns>
    public ChunkFallbackResult Resolve(OverworldEntity entity, Vector2 target, Vector2 playerPos)
    {
        float distToPlayer = entity.Position.DistanceTo(playerPos);
        float distToTarget = entity.Position.DistanceTo(target);

        // 策略 1: 休眠实体 → 直线推进
        if (entity.Lod == OverworldEntity.EntityLod.Hibernated)
        {
            return ResolveDormantAdvance(entity, target, distToTarget);
        }

        // 策略 2: 玩家附近 → 走到已加载边界
        if (distToPlayer < PlayerProximityThreshold)
        {
            return ResolveWalkToBorder(entity, target, distToTarget);
        }

        // 策略 3: 远距离活跃实体 → 低精度直线推进
        return ResolveLinearAdvance(entity, target, distToTarget);
    }

    /// <summary>
    /// 记录寻路失败的详细诊断日志。
    /// </summary>
    public void LogPathFailure(OverworldEntity entity, Vector2 target, Vector2 playerPos)
    {
        bool chunkLoaded = IsTargetChunkLoaded(target);
        float distToPlayer = entity.Position.DistanceTo(playerPos);

        OverworldDiagnostics.LogThrottled(
            OverworldDiagnostics.PrefixAI,
            $"chunk_path_fail_{entity.GetHashCode()}",
            $"chunk_path_failed entity={entity.EntityName}, state={entity.CurrentAIState}, " +
            $"lod={entity.Lod}, from={entity.Position}, target={target}, " +
            $"chunk_loaded={chunkLoaded}, dist_to_player={distToPlayer:F0}",
            cooldownMs: 3000);
    }

    // ========================================
    // 策略实现
    // ========================================

    private ChunkFallbackResult ResolveWalkToBorder(OverworldEntity entity, Vector2 target, float dist)
    {
        if (_chunkManager == null)
            return ChunkFallbackResult.Failed;

        // 计算从实体到目标方向上，最后一个已加载 chunk 内的点
        Vector2 direction = (target - entity.Position).Normalized();
        Vector2 borderPoint = FindLoadedChunkBorder(entity.Position, direction, dist);

        if (borderPoint == entity.Position)
            return new ChunkFallbackResult(false, ChunkFallbackStrategy.WalkToLoadedBorder, Vector2.Zero,
                "already_at_border");

        return new ChunkFallbackResult(true, ChunkFallbackStrategy.WalkToLoadedBorder, borderPoint,
            $"walk_to_border dist={dist:F0}");
    }

    private static ChunkFallbackResult ResolveLinearAdvance(OverworldEntity entity, Vector2 target, float dist)
    {
        float step = System.Math.Min(LinearStepDistance, dist);
        Vector2 direction = (target - entity.Position).Normalized();
        Vector2 intermediate = entity.Position + direction * step;

        return new ChunkFallbackResult(true, ChunkFallbackStrategy.LinearAdvance, intermediate,
            $"linear_advance step={step:F0}");
    }

    private static ChunkFallbackResult ResolveDormantAdvance(OverworldEntity entity, Vector2 target, float dist)
    {
        // 休眠实体直接推进到目标（或推进固定距离）
        const float dormantStep = 600.0f;
        float step = System.Math.Min(dormantStep, dist);
        Vector2 direction = (target - entity.Position).Normalized();
        Vector2 intermediate = entity.Position + direction * step;

        return new ChunkFallbackResult(true, ChunkFallbackStrategy.DormantAdvance, intermediate,
            $"dormant_advance step={step:F0}");
    }

    // ========================================
    // 辅助方法
    // ========================================

    private bool IsTargetChunkLoaded(Vector2 target)
    {
        if (_chunkManager == null) return false;
        var axial = HexOverworldTile.PixelToAxial(target.X, target.Y);
        return _chunkManager.IsLoaded(axial.X, axial.Y);
    }

    private Vector2 FindLoadedChunkBorder(Vector2 from, Vector2 direction, float maxDist)
    {
        if (_chunkManager == null) return from;

        // 从 from 沿 direction 方向步进，找到最后一个已加载 chunk 的边界点
        const float stepSize = 100.0f;
        Vector2 lastLoaded = from;

        for (float d = stepSize; d <= maxDist; d += stepSize)
        {
            Vector2 checkPoint = from + direction * d;
            var axial = HexOverworldTile.PixelToAxial(checkPoint.X, checkPoint.Y);
            if (!_chunkManager.IsLoaded(axial.X, axial.Y))
                break;
            lastLoaded = checkPoint;
        }

        return lastLoaded;
    }
}
