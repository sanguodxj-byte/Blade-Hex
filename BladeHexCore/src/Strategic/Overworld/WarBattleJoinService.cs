using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic.Army;
using BladeHex.Strategic.WorldEvents;

namespace BladeHex.Strategic;

/// <summary>
/// 战斗类型枚举
/// </summary>
public enum WarBattleType
{
    Siege,
    FieldBattle,
    ArmyJoin
}

/// <summary>
/// 玩家可中途加入战役的契机数据
/// </summary>
public class JoinOpportunity
{
    public WarBattleType Type { get; set; }
    public OverworldEntity Attacker { get; set; } = null!;
    
    // Defender 可以是 POI 或者是另一个 Entity (在场战)
    public OverworldPOI? DefenderPoi { get; set; }
    public OverworldEntity? DefenderEntity { get; set; }
    
    public float Distance { get; set; }
    public Army.Army? ArmyRef { get; set; }
}

/// <summary>
/// 战事加入检索服务
/// </summary>
public static class WarBattleJoinService
{
    /// <summary>
    /// 查询玩家周围可加入的战斗与军团。
    /// playerFaction 用于 ArmyJoin 检测;留空时不检测 ArmyJoin,只走 Siege/FieldBattle 路径(向后兼容 M3)。
    /// </summary>
    public static JoinOpportunity? Query(
        Vector2 playerPos,
        List<OverworldEntity> entities,
        List<OverworldPOI> pois,
        string playerFaction = "",
        ArmyRegistry? registry = null,
        float joinRadius = 250.0f,
        WorldEventEngine? engine = null)
    {
        if (entities == null || pois == null) return null;

        // 0. 优先检测可加入的本国军团 (Forming / Marching 状态, 元帅 400px 内)
        if (registry != null && !string.IsNullOrEmpty(playerFaction))
        {
            foreach (var army in registry.All())
            {
                if (army.Faction == playerFaction && (army.State == ArmyState.Forming || army.State == ArmyState.Marching))
                {
                    var marshal = army.Marshal;
                    if (marshal != null && marshal.IsAlive)
                    {
                        float dist = playerPos.DistanceTo(marshal.Position);
                        if (dist <= 400.0f)
                        {
                            return new JoinOpportunity
                            {
                                Type = WarBattleType.ArmyJoin,
                                Attacker = marshal,
                                Distance = dist,
                                ArmyRef = army
                            };
                        }
                    }
                }
            }
        }

        // 1. 检测正在发生的围城战 (正在被围攻的 POI)
        foreach (var poi in pois)
        {
            if (poi.IsUnderSiege && poi.SiegeBy != null && poi.SiegeBy.IsAlive)
            {
                float dist = playerPos.DistanceTo(poi.Position);
                if (dist <= joinRadius)
                {
                    return new JoinOpportunity
                    {
                        Type = WarBattleType.Siege,
                        Attacker = poi.SiegeBy,
                        DefenderPoi = poi,
                        Distance = dist
                    };
                }
            }
        }

        // 2. 检测正在爆发的野外实体交战 (使用 EngagedWith 关系，支持所有实体类型)
        var engagedPairs = new HashSet<(OverworldEntity, OverworldEntity)>();
        for (int i = 0; i < entities.Count; i++)
        {
            var a = entities[i];
            if (!a.IsAlive || a.CurrentAIState != OverworldEntity.AIState.Engaged || a.EngagedWith == null)
                continue;

            var b = a.EngagedWith;
            if (!b.IsAlive || b.CurrentAIState != OverworldEntity.AIState.Engaged)
                continue;

            // 跳过已经处理过的 (a,b) 对
            var key = (a.GetHashCode() < b.GetHashCode()) ? (a, b) : (b, a);
            if (!engagedPairs.Add(key))
                continue;

            // 使用 OverworldHostility 验证敌对关系
            if (!OverworldHostility.AreHostile(a, b, engine))
                continue;

            // 玩家在任一交战方的 joinRadius 内
            float distA = playerPos.DistanceTo(a.Position);
            float distB = playerPos.DistanceTo(b.Position);
            float minDist = Math.Min(distA, distB);

            if (minDist <= joinRadius)
            {
                return new JoinOpportunity
                {
                    Type = WarBattleType.FieldBattle,
                    Attacker = a,
                    DefenderEntity = b,
                    Distance = minDist
                };
            }
        }

        return null;
    }
}
