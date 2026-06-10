// BattlefieldRegistry.cs
// 战场注册表只读快照 — 给前端提供安全的查询接口
//
// 设计目标:
//   - 当前 BattleResolver.Battlefields 在 Core 内部维护
//   - 给前端提供只读快照: id / position / attackers / defenders / progress / duration
//   - View 层不再通过 EngagedWith 自己推断所有战场
//   - 多方战场显示不会丢参与者
//   - WarBattleJoinService 可以基于 Battlefield 快照查询
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Strategic;

/// <summary>
/// 战场只读快照 — 从 Battlefield 提取的不可变数据。
/// 用于前端 View 层渲染和查询，不暴露 Core 层内部引用。
/// </summary>
public readonly struct BattlefieldSnapshot
{
    public string Id { get; }
    public Vector2 Position { get; }
    public IReadOnlyList<BattlefieldParticipantView> Attackers { get; }
    public IReadOnlyList<BattlefieldParticipantView> Defenders { get; }
    public float Progress { get; }
    public float DurationHours { get; }
    public float StartedAtHour { get; }
    public bool IsResolved { get; }

    public BattlefieldSnapshot(
        string id, Vector2 position,
        IReadOnlyList<BattlefieldParticipantView> attackers,
        IReadOnlyList<BattlefieldParticipantView> defenders,
        float progress, float durationHours, float startedAtHour, bool isResolved)
    {
        Id = id;
        Position = position;
        Attackers = attackers;
        Defenders = defenders;
        Progress = progress;
        DurationHours = durationHours;
        StartedAtHour = startedAtHour;
        IsResolved = isResolved;
    }
}

/// <summary>战场参与者只读视图</summary>
public readonly struct BattlefieldParticipantView
{
    public OverworldEntity Entity { get; }
    public string EntityName { get; }
    public string Faction { get; }
    public float CombatPower { get; }
    public int PartySize { get; }
    public bool IsAlive { get; }
    public OverworldEntity.EntityType EntityType { get; }

    public BattlefieldParticipantView(OverworldEntity entity)
    {
        Entity = entity;
        EntityName = entity.EntityName;
        Faction = entity.Faction;
        CombatPower = entity.CombatPower;
        PartySize = entity.PartySize;
        IsAlive = entity.IsAlive;
        EntityType = entity.EntityTypeEnum;
    }
}

/// <summary>
/// 战场注册表 — 从 BattleResolver 提取只读快照。
///
/// 管线位置:
///   BattleResolver.Battlefields → BattlefieldRegistry.GetSnapshots() → View 层
///
/// 用法:
///   var registry = new BattlefieldRegistry();
///   // 每帧:
///   var snapshots = registry.GetSnapshots(battleResolver, currentGameHour);
///   foreach (var bf in snapshots) { ... }
/// </summary>
public sealed class BattlefieldRegistry
{
    /// <summary>
    /// 从 BattleResolver 生成当前所有战场的只读快照列表。
    /// </summary>
    /// <param name="resolver">Core 层战斗结算器</param>
    /// <param name="currentGameHour">当前游戏小时数（用于计算进度）</param>
    public List<BattlefieldSnapshot> GetSnapshots(BattleResolver resolver, float currentGameHour)
    {
        var result = new List<BattlefieldSnapshot>(resolver.Battlefields.Count);

        foreach (var bf in resolver.Battlefields)
        {
            if (bf.IsResolved) continue;

            result.Add(new BattlefieldSnapshot(
                id: bf.BattlefieldId,
                position: bf.Position,
                attackers: bf.Attackers
                    .Select(e => new BattlefieldParticipantView(e))
                    .ToList(),
                defenders: bf.Defenders
                    .Select(e => new BattlefieldParticipantView(e))
                    .ToList(),
                progress: bf.GetProgress(currentGameHour),
                durationHours: bf.DurationHours,
                startedAtHour: bf.StartedAtHour,
                isResolved: bf.IsResolved
            ));
        }

        return result;
    }

    /// <summary>
    /// 查找指定位置附近的战场快照。
    /// </summary>
    public BattlefieldSnapshot? FindNearestBattlefield(
        List<BattlefieldSnapshot> snapshots, Vector2 worldPos, float radius = 300f)
    {
        BattlefieldSnapshot? nearest = null;
        float nearestDist = radius;

        foreach (var bf in snapshots)
        {
            float dist = worldPos.DistanceTo(bf.Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = bf;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 统计当前活跃战场数量。
    /// </summary>
    public static int CountActive(BattleResolver resolver)
    {
        int count = 0;
        foreach (var bf in resolver.Battlefields)
        {
            if (!bf.IsResolved) count++;
        }
        return count;
    }
}
