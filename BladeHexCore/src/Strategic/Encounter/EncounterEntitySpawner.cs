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
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Strategic;

public class EncounterEntitySpawner
{
    private ChunkManager? _chunkManagerRef;

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
    public ChunkManager? ChunkManagerRef
    {
        get => _chunkManagerRef;
        set
        {
            _chunkManagerRef = value;
            TerrainQueryRef = value == null ? null : OverworldTerrainQuery.ForActiveChunks(value);
        }
    }

    /// <summary>活跃大地图地形查询（用于追击路径避障）</summary>
    public OverworldTerrainQuery? TerrainQueryRef { get; set; }

    /// <summary>触发战斗的距离（玩家与实体）</summary>
    public float EncounterDistance = 80.0f;

    /// <summary>实体察觉玩家的视野距离</summary>
    public float EntityVisionRange = 600.0f;

    /// <summary>玩家种族 ID（用于决定冒险者同/异族态度）</summary>
    public int PlayerRaceId { get; set; } = 0;

    /// <summary>不活跃实体池 — 离开活跃区域的实体在此休眠等待复用</summary>
    public DormantEntityPool DormantPool { get; set; } = new();

    /// <summary>同步过来的玩家大地图战斗力</summary>
    public float PlayerCombatPower { get; set; } = 10.0f;

    private readonly PerceptionIntentResolver _perceptionResolver = new();
    private readonly OverworldEntity _playerProxy = new()
    {
        EntityName = "PlayerParty",
        EntityTypeEnum = OverworldEntity.EntityType.Adventurer,
        Faction = "player",
        IsHostileToPlayer = false,
        IsAlive = true,
    };

    private float _timeSinceLastSpawn = 0f;
    private Vector2 _lastPlayerPosition = Vector2.Zero;
    private bool _positionInitialized = false;
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

        // 累积玩家移动距离（首帧仅记录位置，不累积距离）
        if (_positionInitialized)
            _accumulatedDistance += playerPosition.DistanceTo(_lastPlayerPosition);
        _lastPlayerPosition = playerPosition;
        _positionInitialized = true;

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

        // 1. 驱动所有活跃敌对实体的 AI 移动行为（使用 PerceptionIntentResolver 统一判定）
        foreach (var entity in existingEntities)
        {
            if (!entity.IsAlive || !IsHostileToPlayer(entity)) continue;
            if (entity.EntityTypeEnum == OverworldEntity.EntityType.Caravan) continue;
            UpdateChaseAI(entity, playerPosition, existingEntities);
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
            if (!e.IsAlive || !IsHostileToPlayer(e)) continue;
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
            if (e.IsAlive && IsHostileToPlayer(e)
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
            // Caravan 永远不敌对；Adventurer 不敌对（同族/异族 = 友好/中立）；其他保持敌对
            reused.IsHostileToPlayer = entityType switch
            {
                OverworldEntity.EntityType.Caravan => false,
                OverworldEntity.EntityType.Adventurer => false,
                _ => true,
            };
            return reused;
        }

        // 池中无可用实体，新建
        var (templateName, partySize, combatPower, faction) = GetTypeConfig(entityType, playerLevel);

        // 冒险者按 70% 同族 / 30% 异族 随机分配种族
        int raceId = -1;
        if (entityType == OverworldEntity.EntityType.Adventurer)
        {
            bool sameRace = _rng.NextDouble() < 0.7;
            if (sameRace)
            {
                raceId = PlayerRaceId;
            }
            else
            {
                var allRaces = BladeHex.Data.RaceData.GetAllRaces();
                int rid;
                int safety = 8;
                do { rid = (int)allRaces[_rng.Next(allRaces.Length)].raceId; }
                while (rid == PlayerRaceId && --safety > 0);
                raceId = rid;
            }
        }

        // 默认敌对，但 Adventurer/Caravan 是非敌对
        bool hostileDefault = entityType != OverworldEntity.EntityType.Adventurer
                              && entityType != OverworldEntity.EntityType.Caravan;

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
            IsHostileToPlayer = hostileDefault,
            RaceId = raceId,
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

        // 分配 AIStrategy — 按实体类型加权随机
        entity.AIStrategy = PickAIStrategy(entityType);

        return entity;
    }

    /// <summary>按实体类型加权随机分配 AIStrategy</summary>
    private AIStrategyEnum PickAIStrategy(OverworldEntity.EntityType type)
    {
        return type switch
        {
            OverworldEntity.EntityType.BanditParty =>
                // 山贼: 鲁莽/狡诈/恐吓 三选一
                new[] { AIStrategyEnum.Reckless, AIStrategyEnum.Cunning, AIStrategyEnum.Intimidate }[_rng.Next(3)],

            OverworldEntity.EntityType.RobberParty =>
                // 劫匪: 狡诈/谨慎 二选一（偷鸡摸狗型）
                _rng.Next(2) == 0 ? AIStrategyEnum.Cunning : AIStrategyEnum.Cautious,

            OverworldEntity.EntityType.PirateCrew =>
                // 海寇: 鲁莽/恐吓/狂暴 三选一
                new[] { AIStrategyEnum.Reckless, AIStrategyEnum.Intimidate, AIStrategyEnum.Berserk }[_rng.Next(3)],

            OverworldEntity.EntityType.RaidingParty =>
                // 怪物掠夺队: 本能/领地/狂暴 三选一
                new[] { AIStrategyEnum.Instinct, AIStrategyEnum.Territorial, AIStrategyEnum.Berserk }[_rng.Next(3)],

            OverworldEntity.EntityType.EpicMonster =>
                // 史诗怪物: 领地/狂暴 二选一
                _rng.Next(2) == 0 ? AIStrategyEnum.Territorial : AIStrategyEnum.Berserk,

            OverworldEntity.EntityType.Adventurer =>
                // 冒险者: 谨慎/战术 二选一
                _rng.Next(2) == 0 ? AIStrategyEnum.Cautious : AIStrategyEnum.Tactical,

            OverworldEntity.EntityType.Caravan =>
                // 商队: 总是谨慎
                AIStrategyEnum.Cautious,

            _ => AIStrategyEnum.Instinct,
        };
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

    private void UpdateChaseAI(OverworldEntity entity, Vector2 playerPos)
    {
        UpdateChaseAI(entity, playerPos, new List<OverworldEntity>());
    }

    private void UpdateChaseAI(OverworldEntity entity, Vector2 playerPos, List<OverworldEntity> allEntities)
    {
        SyncPlayerProxy(playerPos);

        float playerDistance = entity.Position.DistanceTo(playerPos);
        if (playerDistance <= entity.VisionRange
            && IsHostileToPlayer(entity)
            && CanReactToPlayer(entity))
        {
            var intent = _perceptionResolver.Resolve(entity, _playerProxy, null);
            switch (intent.Type)
            {
                case Intent.IntentType.Chase:
                    entity.CurrentAIState = OverworldEntity.AIState.Chasing;
                    entity.ChaseTarget = _playerProxy;
                    entity.CurrentTacticalTarget = _playerProxy;
                    entity.LastIntentSummary = "追击玩家队伍";
                    break;
                case Intent.IntentType.Flee:
                    entity.CurrentAIState = OverworldEntity.AIState.Fleeing;
                    entity.ChaseTarget = null;
                    entity.CurrentTacticalTarget = _playerProxy;
                    entity.LastIntentSummary = "逃离玩家队伍";
                    break;
                // Intent.None → 战力相当或阈值范围内，不改变状态
                default:
                    break;
            }
        }
        else if (entity.CurrentAIState == OverworldEntity.AIState.Chasing && entity.ChaseTarget == _playerProxy)
        {
            entity.ChaseTarget = null;
            entity.CurrentTacticalTarget = null;
            entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
            entity.LastIntentSummary = "";
            entity.IsMoving = false;
            entity.Path.Clear();
        }
        else if (entity.CurrentAIState == OverworldEntity.AIState.Chasing &&
                 (entity.ChaseTarget == null ||
                  !GodotObject.IsInstanceValid(entity.ChaseTarget) ||
                  !entity.ChaseTarget.IsAlive))
        {
            entity.ChaseTarget = null;
            entity.CurrentTacticalTarget = null;
            entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
            entity.LastIntentSummary = "";
            entity.IsMoving = false;
            entity.Path.Clear();
        }

        // 1. 追击决策移动 (Chasing)
        if (entity.CurrentAIState == OverworldEntity.AIState.Chasing
            && entity.ChaseTarget != null
            && GodotObject.IsInstanceValid(entity.ChaseTarget)
            && entity.ChaseTarget.IsAlive)
        {
            Vector2 targetPos = entity.ChaseTarget.Position;
            entity.TargetPosition = targetPos;
            entity.IsMoving = true;

            entity.Path.Clear();
            var chasePath = BuildChasePath(entity.Position, targetPos);
            foreach (var p in chasePath)
                entity.Path.Add(p);
        }
        // 2. 逃跑决策移动 (Fleeing)
        else if (entity.CurrentAIState == OverworldEntity.AIState.Fleeing)
        {
            // 寻找使自身感到最紧迫威胁的最近敌对实体
            var (threat, _) = ScanNearestThreat(entity, allEntities);
            if (threat == null && playerDistance <= entity.VisionRange && IsHostileToPlayer(entity))
                threat = _playerProxy;
            if (threat != null)
            {
                // 反向移动：避障逃离威胁目标
                Vector2 fleeDir = entity.Position - threat.Position;
                if (fleeDir.LengthSquared() <= 0.001f)
                    fleeDir = new Vector2(1, 0);
                fleeDir = fleeDir.Normalized();
                Vector2 fleeTarget = entity.Position + fleeDir * 500.0f;
                entity.TargetPosition = fleeTarget;
                entity.IsMoving = true;

                entity.Path.Clear();
                var fleePath = BuildChasePath(entity.Position, fleeTarget);
                foreach (var p in fleePath)
                    entity.Path.Add(p);
            }
            else
            {
                // 无威胁时跑回 HomePosition
                if (entity.Position.DistanceTo(entity.HomePosition) > 50.0f)
                {
                    entity.TargetPosition = entity.HomePosition;
                    entity.IsMoving = true;
                    entity.Path.Clear();
                    var returnPath = BuildChasePath(entity.Position, entity.HomePosition);
                    foreach (var p in returnPath)
                        entity.Path.Add(p);
                }
                else
                {
                    entity.CurrentAIState = OverworldEntity.AIState.Patrolling;
                    entity.IsMoving = false;
                    entity.Path.Clear();
                }
            }
        }
        // 3. 默认巡逻移动 (Patrolling)
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

    private (OverworldEntity? threat, float distance) ScanNearestThreat(OverworldEntity entity, List<OverworldEntity> allEntities)
    {
        OverworldEntity? best = null;
        float bestDist = float.MaxValue;
        foreach (var other in allEntities)
        {
            if (other == entity || !other.IsAlive) continue;
            float d = entity.Position.DistanceTo(other.Position);
            if (d > entity.VisionRange) continue;

            if (!OverworldHostility.AreHostile(entity, other)) continue;

            if (d < bestDist)
            {
                bestDist = d;
                best = other;
            }
        }
        return (best, bestDist != float.MaxValue ? bestDist : 0f);
    }

    private void SyncPlayerProxy(Vector2 playerPos)
    {
        _playerProxy.Position = playerPos;
        _playerProxy.HomePosition = playerPos;
        _playerProxy.CombatPower = Math.Max(1f, PlayerCombatPower);
        _playerProxy.PartySize = Math.Max(1, (int)MathF.Ceiling(_playerProxy.CombatPower / 10f));
        _playerProxy.IsAlive = true;
    }

    private bool IsHostileToPlayer(OverworldEntity entity)
        => entity.EntityTypeEnum != OverworldEntity.EntityType.Caravan
           && OverworldHostility.AreHostile(entity, _playerProxy);

    private static bool CanReactToPlayer(OverworldEntity entity)
        => entity.CurrentAIState == OverworldEntity.AIState.Idle
        || entity.CurrentAIState == OverworldEntity.AIState.Patrolling
        || (entity.CurrentAIState == OverworldEntity.AIState.Chasing && entity.ChaseTarget?.Faction == "player");

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
        // 未加载的 tile 视为可通行（实体在视野外会被收容）
        return TerrainQueryRef?.IsPassableAtPixel(pos, unknownIsPassable: true) ?? true;
    }
}
