using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>World-space position the player should approach before opening the join panel.</summary>
    public Vector2 WorldPosition { get; set; } = new(float.NaN, float.NaN);

    public bool HasWorldPosition => !float.IsNaN(WorldPosition.X) && !float.IsNaN(WorldPosition.Y);

    // ── NvN 多方战场扩展 ──
    /// <summary>战场唯一 ID（用于战后回写清理）</summary>
    public string BattlefieldId { get; set; } = "";

    /// <summary>攻击方全实体列表（NvN 多方战场）</summary>
    public List<OverworldEntity> Attackers { get; set; } = new();

    /// <summary>防御方全实体列表（NvN 多方战场）</summary>
    public List<OverworldEntity> Defenders { get; set; } = new();

    /// <summary>攻击方总战力</summary>
    public float AttackerTotalPower { get; set; }

    /// <summary>防御方总战力</summary>
    public float DefenderTotalPower { get; set; }

    /// <summary>获取全部参战实体（攻防合并）</summary>
    public IEnumerable<OverworldEntity> AllParticipants()
    {
        foreach (var e in Attackers) yield return e;
        foreach (var e in Defenders) yield return e;
        if (Attacker != null && !Attackers.Contains(Attacker)) yield return Attacker;
        if (DefenderEntity != null && !Defenders.Contains(DefenderEntity)) yield return DefenderEntity;
    }
}

/// <summary>
/// 战事加入检索服务
/// </summary>
public static class WarBattleJoinService
{
    /// <summary>
    /// 查询玩家周围可加入的战斗与军团。
    /// playerFaction 用于 ArmyJoin 检测;留空时不检测 ArmyJoin,只走 Siege/FieldBattle 路径(向后兼容 M3)。
    /// registry/battleResolver 用于 BattlefieldRegistry 快照查询（NvN 多方战场）。
    /// </summary>
    public static JoinOpportunity? Query(
        Vector2 playerPos,
        List<OverworldEntity> entities,
        List<OverworldPOI> pois,
        string playerFaction = "",
        ArmyRegistry? registry = null,
        float joinRadius = 250.0f,
        WorldEventEngine? engine = null,
        BattlefieldRegistry? battlefieldRegistry = null,
        BattleResolver? battleResolver = null,
        float currentGameHour = 0f)
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
                                WorldPosition = marshal.Position,
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
                        WorldPosition = poi.Position,
                        Distance = dist
                    };
                }
            }
        }

        // 2a. 优先路径：BattlefieldRegistry 快照（NvN 多方战场）
        if (battlefieldRegistry != null && battleResolver != null)
        {
            try
            {
                var snapshots = battlefieldRegistry.GetSnapshots(battleResolver, currentGameHour);
                foreach (var bf in snapshots)
                {
                    if (playerPos.DistanceTo(bf.Position) > joinRadius) continue;
                    if (bf.Attackers.Count == 0 || bf.Defenders.Count == 0) continue;

                    var atkEntities = new List<OverworldEntity>();
                    var defEntities = new List<OverworldEntity>();
                    foreach (var p in bf.Attackers)
                    {
                        var e = ResolveParticipantEntity(entities, p);
                        if (e != null) atkEntities.Add(e);
                    }
                    foreach (var p in bf.Defenders)
                    {
                        var e = ResolveParticipantEntity(entities, p);
                        if (e != null) defEntities.Add(e);
                    }

                    var primaryAtk = atkEntities.OrderByDescending(e => e.CombatPower * e.PartySize).FirstOrDefault();
                    var primaryDef = defEntities.OrderByDescending(e => e.CombatPower * e.PartySize).FirstOrDefault();
                    if (primaryAtk == null || primaryDef == null) continue;

                    return new JoinOpportunity
                    {
                        Type = WarBattleType.FieldBattle,
                        BattlefieldId = bf.Id,
                        Attacker = primaryAtk,
                        DefenderEntity = primaryDef,
                        Attackers = atkEntities,
                        Defenders = defEntities,
                        AttackerTotalPower = atkEntities.Sum(e => e.CombatPower * e.PartySize),
                        DefenderTotalPower = defEntities.Sum(e => e.CombatPower * e.PartySize),
                        WorldPosition = bf.Position,
                        Distance = playerPos.DistanceTo(bf.Position)
                    };
                }
            }
            catch
            {
                // Registry 异常时降级到 EngagedWith pair 扫描
            }
        }

        // 2b. 降级路径：EngagedWith pair 扫描
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
                    WorldPosition = (a.Position + b.Position) * 0.5f,
                    Distance = minDist
                };
            }
        }

        return null;
    }

    private static OverworldEntity? ResolveParticipantEntity(
        List<OverworldEntity> entities,
        BattlefieldParticipantView participant)
    {
        if (participant.Entity.IsAlive && entities.Contains(participant.Entity))
            return participant.Entity;

        return entities.FirstOrDefault(e => e.EntityName == participant.EntityName && e.IsAlive);
    }
}
