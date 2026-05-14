// MovementProcessor.cs
// 每帧移动处理器 — 处理所有实体的移动逻辑
// 从 OverworldEntityManager 拆出的 Core 层组件
using Godot;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 每帧移动处理器 — 管理所有实体的帧级移动更新
/// </summary>
public class MovementProcessor
{
    private const float CHASE_SPEED_MULT = 1.1f;

    /// <summary>每帧更新所有实体移动</summary>
    public void TickMovement(float delta, List<OverworldEntity> entities, System.Action<OverworldEntity>? onReachedDestination)
    {
        foreach (var entity in entities)
        {
            if (!entity.IsMoving || !entity.IsAlive) continue;

            if (entity.Path.Count == 0)
            {
                entity.IsMoving = false;
                onReachedDestination?.Invoke(entity);
                continue;
            }

            Vector2 targetPos = entity.Path[0];
            Vector2 dir = (targetPos - entity.Position).Normalized();
            float speed = entity.MoveSpeed;

            if (entity.CurrentAIState == OverworldEntity.AIState.Chasing)
                speed *= CHASE_SPEED_MULT;

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