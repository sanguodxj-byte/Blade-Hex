// MovementProcessor.cs
// 每帧移动处理器 — 管理所有实体的帧级移动更新
// 从 OverworldEntityManager 拆出的 Core 层组件
//
// 速度计算委托到 EntitySpeedCalculator (提取为独立组件供 UI 展示分解)
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 每帧移动处理器 — 管理所有实体的帧级移动更新
/// </summary>
public class MovementProcessor
{
    private ChunkManager? _chunkManagerRef;

    /// <summary>地形查询源（由 OverworldEntityManager 注入）</summary>
    public ChunkManager? ChunkManagerRef
    {
        get => _chunkManagerRef;
        set
        {
            _chunkManagerRef = value;
            TerrainQueryRef = value == null ? null : OverworldTerrainQuery.ForActiveChunks(value);
        }
    }

    /// <summary>活跃大地图地形查询（由 ChunkManagerRef 派生，也可直接注入）</summary>
    public OverworldTerrainQuery? TerrainQueryRef { get; set; }

    /// <summary>POI 控制区管理器（由 OverworldEntityManager 注入）</summary>
    public ZoneOfControlManager? ZocManagerRef { get; set; }

    /// <summary>
    /// 计算实体实际移速 = 委托到 EntitySpeedCalculator
    /// </summary>
    private float CalculateEffectiveSpeed(OverworldEntity entity, Vector2 position)
    {
        return EntitySpeedCalculator.CalculateSpeed(entity, position, TerrainQueryRef, ZocManagerRef);
    }

    /// <summary>每帧更新所有实体移动</summary>
    public void TickMovement(float delta, List<OverworldEntity> entities, System.Action<OverworldEntity>? onReachedDestination)
    {
        foreach (var entity in entities)
        {
            if (!entity.IsMoving || !entity.IsAlive || entity.Lod == OverworldEntity.EntityLod.Hibernated) continue;

            // 交战中 → 强制停止移动
            if (entity.CurrentAIState == OverworldEntity.AIState.Engaged)
            {
                entity.IsMoving = false;
                entity.Path.Clear();
                continue;
            }

            if (entity.Path.Count == 0)
            {
                entity.IsMoving = false;
                onReachedDestination?.Invoke(entity);
                continue;
            }

            Vector2 targetPos = entity.Path[0];
            Vector2 dir = (targetPos - entity.Position).Normalized();

            // 使用统一速度计算（含地形/ZoC/策略修正）
            float speed = CalculateEffectiveSpeed(entity, entity.Position);

            float step = speed * delta;

            if (step >= entity.Position.DistanceTo(targetPos))
            {
                entity.Position = targetPos;
                entity.Path.RemoveAt(0);
                if (entity.Path.Count == 0)
                {
                    entity.IsMoving = false;
                    onReachedDestination?.Invoke(entity);
                }
            }
            else
            {
                entity.Position += dir * step;
            }
        }
    }
}
