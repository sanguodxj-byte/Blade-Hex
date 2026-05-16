// EncounterEntitySpawner.cs
// 大地图遭遇实体生成器 — 周期性在玩家周围生成会移动的敌对实体
//
// 设计：
// - 玩家移动一段距离后，按生态/危险度概率在视野外生成 OverworldEntity
// - 实体类型：RaidingParty / Adventurer / EpicMonster 等
// - 实体有完整 AI 状态、移动路径，由 ChaseAI 驱动追击玩家
// - 玩家靠近实体时触发战斗
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.Strategic;

public class EncounterEntitySpawner
{
    /// <summary>每次生成尝试的最少间隔（秒）</summary>
    public float SpawnIntervalSec = 8.0f;

    /// <summary>玩家累积移动多少距离后允许 spawn</summary>
    public float MinDistanceTraveled = 600.0f;

    /// <summary>同时存在的最大敌对实体数</summary>
    public int MaxActiveEntities = 5;

    /// <summary>生成距离玩家的最远像素距离（视野外）</summary>
    public float SpawnDistanceMin = 800.0f;
    public float SpawnDistanceMax = 1600.0f;

    /// <summary>Chunk 模式寻路引用（由外部注入，用于追击路径避障）</summary>
    public ChunkManager? ChunkManagerRef { get; set; }

    /// <summary>触发战斗的距离（玩家与实体）</summary>
    public float EncounterDistance = 80.0f;

    /// <summary>实体察觉玩家的视野距离</summary>
    public float EntityVisionRange = 600.0f;

    /// <summary>不活跃实体池 — 离开活跃区域的实体在此休眠等待复用</summary>
    public DormantEntityPool DormantPool { get; set; } = new();

    private float _timeSinceLastSpawn = 0f;
    private Vector2 _lastPlayerPosition = Vector2.Zero;
    private float _accumulatedDistance = 0f;
    private readonly Random _rng = new();

    /// <summary>
    /// 每帧调用：检查是否需要 spawn 新实体、更新现有实体追击 AI
    /// </summary>
    /// <returns>新生成的实体列表</returns>
    public List<OverworldEntity> Tick(
        float delta,
        Vector2 playerPosition,
        List<OverworldEntity> existingEntities,
        int playerLevel,
        int currentDay)
    {
        return Tick(delta, playerPosition, existingEntities, playerLevel, currentDay, null);
    }

    /// <summary>
    /// 每帧调用（带 POI 列表，用于商队寻路）
    /// </summary>
    public List<OverworldEntity> Tick(
        float delta,
        Vector2 playerPosition,
        List<OverworldEntity> existingEntities,
        int playerLevel,
        int currentDay,
        List<OverworldPOI>? worldPois)
    {
        var newlySpawned = new List<OverworldEntity>();

        // 累积玩家移动距离
        if (_lastPlayerPosition != Vector2.Zero)
            _accumulatedDistance += playerPosition.DistanceTo(_lastPlayerPosition);
        _lastPlayerPosition = playerPosition;

        _timeSinceLastSpawn += delta;

        // 检查 spawn 条件
        if (_timeSinceLastSpawn >= SpawnIntervalSec
            && _accumulatedDistance >= MinDistanceTraveled
            && CountActiveHostiles(existingEntities) < MaxActiveEntities)
        {
            var entity = TrySpawnEntity(playerPosition, playerLevel, currentDay, worldPois);
            if (entity != null)
            {
                newlySpawned.Add(entity);
                _timeSinceLastSpawn = 0f;
                _accumulatedDistance = 0f;
            }
        }

        // 更新所有敌对实体的追击 AI
        foreach (var entity in existingEntities)
        {
            if (!entity.IsAlive || !entity.IsHostileToPlayer) continue;
            if (entity.EntityTypeEnum == OverworldEntity.EntityType.Caravan) continue;
            UpdateChaseAI(entity, playerPosition);
        }

        return newlySpawned;
    }

    /// <summary>检查是否触发战斗（玩家靠近敌对实体）</summary>
    public OverworldEntity? CheckEncounter(Vector2 playerPosition, List<OverworldEntity> entities)
    {
        OverworldEntity? closest = null;
        float closestDist = EncounterDistance;
        foreach (var e in entities)
        {
            if (!e.IsAlive || !e.IsHostileToPlayer) continue;
            if (e.EntityTypeEnum == OverworldEntity.EntityType.Caravan) continue;
            float d = playerPosition.DistanceTo(e.Position);
            if (d <= closestDist)
            {
                closestDist = d;
                closest = e;
            }
        }
        return closest;
    }

    private int CountActiveHostiles(List<OverworldEntity> entities)
    {
        int n = 0;
        foreach (var e in entities)
        {
            if (e.IsAlive && e.IsHostileToPlayer
                && e.EntityTypeEnum != OverworldEntity.EntityType.Caravan)
                n++;
        }
        return n;
    }

    /// <summary>在玩家周围视野外生成一个敌对实体（优先从不活跃池复用）</summary>
    private OverworldEntity? TrySpawnEntity(Vector2 playerPos, int playerLevel, int currentDay, List<OverworldPOI>? worldPois)
    {
        // 随机方向
        float angle = (float)(_rng.NextDouble() * Math.PI * 2);
        float distance = (float)(SpawnDistanceMin + _rng.NextDouble() * (SpawnDistanceMax - SpawnDistanceMin));
        Vector2 spawnPos = playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

        // 选取实体类型
        var entityType = PickEntityType();

        // 优先从不活跃池复用
        var reused = DormantPool.TryReuse(entityType, spawnPos, currentDay);
        if (reused != null)
        {
            // 复用成功：更新等级缩放（保持与玩家等级同步）
            reused.PartyLevel = Mathf.Max(1, playerLevel + _rng.Next(-1, 2));
            reused.CurrentAIState = OverworldEntity.AIState.Patrolling;
            reused.VisionRange = EntityVisionRange;
            reused.IsHostileToPlayer = entityType != OverworldEntity.EntityType.Caravan;
            return reused;
        }

        // 池中无可用实体，新建
        var (templateName, partySize, combatPower, faction) = GetTypeConfig(entityType, playerLevel);

        var entity = new OverworldEntity
        {
            EntityName = templateName,
            EntityTypeEnum = entityType,
            Position = spawnPos,
            HomePosition = spawnPos,
            TerritoryCenter = spawnPos,
            TerritoryRadius = 800f,
            MoveSpeed = 80f + (float)_rng.NextDouble() * 40f, // 80-120 px/s（玩家约 300）
            PartySize = partySize,
            PartyLevel = Mathf.Max(1, playerLevel + _rng.Next(-1, 2)),
            CombatPower = combatPower,
            Faction = faction,
            IsHostileToPlayer = true,
            VisionRange = EntityVisionRange,
            CurrentAIState = OverworldEntity.AIState.Patrolling,
            IsAlive = true,
        };

        // RaidingParty 根据名称设置对应的怪物种族
        if (entityType == OverworldEntity.EntityType.RaidingParty)
        {
            var race = entity.EntityName switch
            {
                "哥布林掠夺队" => OverworldPOI.SettlementRace.Goblin,
                "狗头人小队" => OverworldPOI.SettlementRace.Kobold,
                "牛头人战团" => OverworldPOI.SettlementRace.Minotaur,
                _ => OverworldPOI.SettlementRace.Goblin,
            };
            entity.SourceSettlement = new OverworldPOI
            {
                PoiName = entity.EntityName,
                SettlementRaceValue = race,
                ThreatLevel = playerLevel * 0.3f,
            };
            // 怪物类型标记（用于描述和战斗模板选择）
            entity.MonsterType = entity.EntityName switch
            {
                "亡灵游荡者" => "undead",
                "野兽群" => "beast",
                "巨魔" => "troll",
                _ => "goblinoid",
            };
        }

        // 史诗怪物固定参数
        if (entityType == OverworldEntity.EntityType.EpicMonster)
        {
            entity.MonsterType = _rng.Next(2) == 0 ? "dragon" : "ancient_golem";
            entity.PartyLevel = Mathf.Max(playerLevel + 3, 5);
            entity.MoveSpeed *= 0.6f; // 巨怪慢
        }

        // 商队：选一个随机城镇/村庄作为目的地，设置路径
        if (entityType == OverworldEntity.EntityType.Caravan && worldPois != null && worldPois.Count > 0)
        {
            entity.IsHostileToPlayer = false;
            entity.CurrentAIState = OverworldEntity.AIState.MovingToTarget;
            entity.MoveSpeed = 60f + (float)_rng.NextDouble() * 30f; // 商队慢 60-90

            // 选一个随机城镇/村庄作为目的地
            var towns = new List<OverworldPOI>();
            foreach (var poi in worldPois)
            {
                if (poi.PoiTypeEnum == OverworldPOI.POIType.Town || poi.PoiTypeEnum == OverworldPOI.POIType.Village)
                    towns.Add(poi);
            }
            if (towns.Count > 0)
            {
                var dest = towns[_rng.Next(towns.Count)];
                entity.TargetPosition = dest.Position;
                entity.Path.Clear();
                entity.Path.Add(dest.Position);
                entity.IsMoving = true;
                entity.DestinationTown = dest;
            }
        }

        return entity;
    }

    private OverworldEntity.EntityType PickEntityType()
    {
        // 加权随机 — 人类系和怪物系分离
        // 注意：LordArmy 不在野外随机生成（领主军队由国家系统派遣，不是随机遭遇）
        // 人类系: BanditParty(25%) + RobberParty(10%) = 35%
        // 怪物系: RaidingParty(35%) = 35%（内部再按种族细分）
        // 中立: Adventurer(15%) + Caravan(10%) = 25%
        // 稀有: EpicMonster(5%) = 5%
        int roll = _rng.Next(100);
        if (roll < 35) return OverworldEntity.EntityType.RaidingParty;   // 怪物掠夺队
        if (roll < 60) return OverworldEntity.EntityType.BanditParty;    // 人类山贼
        if (roll < 70) return OverworldEntity.EntityType.RobberParty;    // 人类劫匪
        if (roll < 85) return OverworldEntity.EntityType.Adventurer;     // 中立冒险者
        if (roll < 95) return OverworldEntity.EntityType.Caravan;        // 中立商队
        return OverworldEntity.EntityType.EpicMonster;                    // 稀有巨兽
    }

    private (string name, int partySize, float combatPower, string faction)
        GetTypeConfig(OverworldEntity.EntityType type, int playerLevel)
    {
        return type switch
        {
            // 怪物系 — 名称由 SettlementRace 决定（在 TrySpawnEntity 中设置）
            OverworldEntity.EntityType.RaidingParty =>
                (PickMonsterPartyName(), 3 + _rng.Next(4), 10f + playerLevel * 5f, "monster"),

            // 人类系 — 明确的人类敌对势力
            OverworldEntity.EntityType.BanditParty =>
                (PickHumanBanditName(), 3 + _rng.Next(3), 8f + playerLevel * 4f, "bandit"),
            OverworldEntity.EntityType.RobberParty =>
                (PickHumanRobberName(), 2 + _rng.Next(2), 7f + playerLevel * 3f, "bandit"),
            OverworldEntity.EntityType.LordArmy =>
                ("敌对巡逻队", 5 + _rng.Next(4), 15f + playerLevel * 6f, "hostile_lord"),
            OverworldEntity.EntityType.PirateCrew =>
                ("海寇", 3 + _rng.Next(3), 9f + playerLevel * 4f, "pirate"),

            // 中立
            OverworldEntity.EntityType.Adventurer =>
                ("冒险者队伍", 3 + _rng.Next(2), 15f + playerLevel * 6f, "neutral"),
            OverworldEntity.EntityType.Caravan =>
                ("商队", 3 + _rng.Next(2), 5f, "merchant"),

            // 稀有
            OverworldEntity.EntityType.EpicMonster =>
                ("巨兽", 1, 30f + playerLevel * 10f, "monster"),

            _ => ("流浪者", 2, 5f, "neutral"),
        };
    }

    /// <summary>随机选择怪物掠夺队种族和名称</summary>
    private string PickMonsterPartyName()
    {
        int roll = _rng.Next(100);
        if (roll < 40) return "哥布林掠夺队";
        if (roll < 60) return "狗头人小队";
        if (roll < 75) return "牛头人战团";
        if (roll < 85) return "亡灵游荡者";
        if (roll < 95) return "野兽群";
        return "巨魔";
    }

    /// <summary>随机选择人类山贼名称</summary>
    private string PickHumanBanditName()
    {
        string[] names = ["黑鸦团", "断刃帮", "荒野劫匪", "落魄佣兵", "逃兵小队", "亡命之徒"];
        return names[_rng.Next(names.Length)];
    }

    /// <summary>随机选择人类劫匪名称</summary>
    private string PickHumanRobberName()
    {
        string[] names = ["路霸", "拦路贼", "流寇", "马贼"];
        return names[_rng.Next(names.Length)];
    }

    /// <summary>
    /// 更新单个实体的追击 AI：
    /// - 视野内 → 使用简化寻路追击玩家（避免穿模）
    /// - 视野外 → 巡逻或返回基地
    /// </summary>
    private void UpdateChaseAI(OverworldEntity entity, Vector2 playerPos)
    {
        float distToPlayer = entity.Position.DistanceTo(playerPos);

        if (distToPlayer <= entity.VisionRange)
        {
            // 进入追击状态
            entity.CurrentAIState = OverworldEntity.AIState.Chasing;
            entity.TargetPosition = playerPos;
            entity.IsMoving = true;

            // 使用多点追击路径（朝玩家方向步进，每 N 格采样一次）
            // 避免直线穿越不可通行地形
            entity.Path.Clear();
            var chasePath = BuildChasePath(entity.Position, playerPos);
            foreach (var p in chasePath)
                entity.Path.Add(p);
        }
        else if (entity.CurrentAIState == OverworldEntity.AIState.Chasing)
        {
            // 玩家逃出视野 → 切换到巡逻
            entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
            entity.IsMoving = false;
            entity.Path.Clear();
        }
        else if (!entity.IsMoving && entity.CurrentAIState == OverworldEntity.AIState.Patrolling)
        {
            // 巡逻：在领地范围内随机走动
            float angle = (float)(_rng.NextDouble() * Math.PI * 2);
            float dist = (float)(_rng.NextDouble() * entity.PatrolRadius);
            Vector2 patrolTarget = entity.HomePosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            entity.Path.Clear();
            entity.Path.Add(patrolTarget);
            entity.IsMoving = true;
            entity.TargetPosition = patrolTarget;
        }
    }

    /// <summary>
    /// 构建追击路径 — 沿直线方向每 200px 采样，跳过不可通行点。
    /// 比完整 A* 轻量，但避免了直线穿山穿水的问题。
    /// 如果中间有障碍，尝试绕行（左右偏移 60°）。
    /// </summary>
    private List<Vector2> BuildChasePath(Vector2 from, Vector2 to)
    {
        var path = new List<Vector2>();
        float totalDist = from.DistanceTo(to);
        const float stepSize = 200.0f;

        if (totalDist <= stepSize)
        {
            path.Add(to);
            return path;
        }

        Vector2 dir = (to - from).Normalized();
        int steps = (int)(totalDist / stepSize);
        Vector2 lastValid = from;

        for (int i = 1; i <= steps; i++)
        {
            Vector2 candidate = from + dir * (stepSize * i);

            // 简单检查：如果该点不可通行，尝试左右偏移
            if (!IsPositionPassable(candidate))
            {
                // 尝试左偏 60°
                Vector2 leftDir = dir.Rotated(Mathf.Pi / 3.0f);
                Vector2 leftCandidate = lastValid + leftDir * stepSize;
                if (IsPositionPassable(leftCandidate))
                {
                    path.Add(leftCandidate);
                    lastValid = leftCandidate;
                    continue;
                }

                // 尝试右偏 60°
                Vector2 rightDir = dir.Rotated(-Mathf.Pi / 3.0f);
                Vector2 rightCandidate = lastValid + rightDir * stepSize;
                if (IsPositionPassable(rightCandidate))
                {
                    path.Add(rightCandidate);
                    lastValid = rightCandidate;
                    continue;
                }

                // 都不行，跳过这个点（实体会在 lastValid 停下，下帧重新计算）
                break;
            }
            else
            {
                path.Add(candidate);
                lastValid = candidate;
            }
        }

        // 最终目标点
        if (path.Count == 0 || path[^1].DistanceTo(to) > stepSize)
            path.Add(to);

        return path;
    }

    /// <summary>检查像素位置是否可通行（用于追击路径构建）</summary>
    private bool IsPositionPassable(Vector2 pos)
    {
        if (ChunkManagerRef != null)
        {
            var axial = HexOverworldTile.PixelToAxial(pos.X, pos.Y);
            var tile = ChunkManagerRef.GetTile(axial.X, axial.Y);
            // 未加载的 tile 视为可通行（实体在视野外会被收容）
            if (tile == null) return true;
            return tile.IsPassable;
        }
        return true;
    }
}
