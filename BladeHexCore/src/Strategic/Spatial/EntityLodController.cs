using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 控制大地图实体 LOD 级别的状态转换器，在 OnDayPassed 开头调用。
/// </summary>
public static class EntityLodController
{
    private const float ACTIVE_THRESHOLD = 5000f;     // 休眠 -> 激活的距离阈值
    private const float HIBERNATE_THRESHOLD = 5500f;  // 激活 -> 休眠的距离阈值

    /// <summary>
    /// 根据玩家当前位置更新所有实体的 LOD 级别（休眠/激活状态）
    /// </summary>
    public static void Update(IEnumerable<OverworldEntity> entities, Vector2 playerPos)
    {
        foreach (var entity in entities)
        {
            if (!entity.IsAlive) continue;

            float dist = entity.Position.DistanceTo(playerPos);

            if (entity.Lod == OverworldEntity.EntityLod.Hibernated && dist <= ACTIVE_THRESHOLD)
            {
                entity.Lod = OverworldEntity.EntityLod.Active;
            }
            else if (entity.Lod == OverworldEntity.EntityLod.Active && dist >= HIBERNATE_THRESHOLD)
            {
                entity.Lod = OverworldEntity.EntityLod.Hibernated;
            }
        }
    }
}
