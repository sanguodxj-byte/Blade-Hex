// EntitySpawner.cs
// 实体生成器 — 创建初始实体模板和运行时新实体
// 从 WorldGenerator 拆出：GenerateEntityTemplates/GenerateInitialEntities/CreateRaidingParty
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 实体生成器 — 负责创建初始世界实体模板和运行时新实体
/// 包含冒险者、掠夺队、商队、史诗怪物、领主军队等所有实体类型的生成逻辑
/// </summary>
[GlobalClass]
public partial class EntitySpawner : RefCounted
{
    // ========================================
    // 依赖
    // ========================================

    /// <summary>区域注册表（用于位置查找）</summary>
    public WorldRegionRegistry? Registry { get; set; }

    /// <summary>旧 HexGrid 引用（迁移过渡期，用于寻路生成位置）</summary>
    public Map.HexOverworldGrid? HexGrid { get; set; }
    public Map.HexOverworldGenerator? HexGen { get; set; }

    private static readonly Random _random = new();

    // ========================================
    // 批量生成
    // ========================================

    /// <summary>
    /// 生成初始实体模板列表
    /// 包含：冒险者、掠夺队、商队、史诗怪物
    /// </summary>
    public List<OverworldEntity> GenerateEntityTemplates(
        List<OverworldPOI> pois,
        WorldRegionRegistry registry,
        Map.HexOverworldGrid? grid = null,
        Map.HexOverworldGenerator? gen = null)
    {
        HexGrid = grid;
        HexGen = gen;
        Registry = registry;

        var templates = new List<OverworldEntity>();

        GenerateAdventurers(pois, templates);
        GenerateRaidingParties(pois, templates);
        GenerateCaravans(pois, templates);
        GenerateEpicMonsters(pois, templates);

        return templates;
    }

    // ========================================
    // 冒险者
    // ========================================

    private void GenerateAdventurers(List<OverworldPOI> pois, List<OverworldEntity> templates)
    {
        string[] advNames = ["铜剑团", "灰烬小队", "银叶猎团"];
        var humanTowns = pois.Where(p => p.OwningFaction == "kingdom" && p.PoiTypeEnum == OverworldPOI.POIType.Town).ToList();

        for (int i = 0; i < 2 + _random.Next(2); i++)
        {
            Vector2 pos = Vector2.Zero;
            if (HexGrid != null && humanTowns.Count > 0)
            {
                var baseTown = humanTowns[i % humanTowns.Count];
                var tile = HexGrid.FindPassableNearPixel(
                    baseTown.Position.X + (float)(_random.NextDouble() * 400 - 200),
                    baseTown.Position.Y + (float)(_random.NextDouble() * 400 - 200), 5);
                pos = tile?.PixelPos ?? baseTown.Position;
            }
            else if (Registry != null)
            {
                pos = Registry.FindPositionInRegion(Registry.GetRegionByName("中央平原"), pois, 100.0f);
            }

            var entity = new OverworldEntity
            {
                EntityName = i < advNames.Length ? advNames[i] : "无名冒险者",
                EntityTypeEnum = OverworldEntity.EntityType.Adventurer,
                Position = pos,
                HomePosition = pos,
                PartySize = 2 + _random.Next(5),
                PartyLevel = 1 + _random.Next(3)
            };
            entity.CombatPower = entity.PartySize * entity.PartyLevel * 2.0f;
            entity.MoveSpeed = 180.0f;
            entity.PatrolRadius = 400.0f;
            entity.VisionRange = 350.0f;
            entity.IsHostileToPlayer = false;
            entity.Faction = "adventurers";
            entity.AdventurerType = i switch { 0 => "novice", 1 => "veteran", _ => "elite" };
            entity.GoldCarried = 30 + _random.Next(100);
            entity.AIStrategy = _random.Next(2) == 0 ? AIStrategyEnum.Cautious : AIStrategyEnum.Tactical;
            templates.Add(entity);
        }
    }

    // ========================================
    // 掠夺队
    // ========================================

    private void GenerateRaidingParties(List<OverworldPOI> pois, List<OverworldEntity> templates)
    {
        foreach (var poi in pois)
            if (poi.PoiTypeEnum == OverworldPOI.POIType.Settlement && poi.ShouldSpawnRaidParty())
            {
                var party = CreateRaidingParty(poi);
                if (party != null)
                {
                    templates.Add(party);
                    poi.OnRaidPartySpawned();
                }
            }
    }

    /// <summary>
    /// 为指定聚落创建一支掠夺队
    /// </summary>
    public OverworldEntity? CreateRaidingParty(OverworldPOI source)
    {
        var villages = Registry != null
            ? new List<OverworldPOI>() // 需要 POI 列表，调用方应传入
            : new List<OverworldPOI>();

        // 从 source 的邻居中找最近村庄
        return CreateRaidingPartyWithTargets(source, villages);
    }

    /// <summary>
    /// 为指定聚落创建一支掠夺队（带完整 POI 列表）
    /// </summary>
    public OverworldEntity? CreateRaidingPartyWithTargets(OverworldPOI source, List<OverworldPOI> allPois)
    {
        var villages = allPois.Where(p => p.PoiTypeEnum == OverworldPOI.POIType.Village).ToList();
        if (villages.Count == 0) return null;

        var closestVillage = villages.OrderBy(v => source.Position.DistanceTo(v.Position)).First();

        var entityType = OverworldEntity.EntityType.RaidingParty;
        if (source.SettlementRaceValue == OverworldPOI.SettlementRace.Bandit) entityType = OverworldEntity.EntityType.BanditParty;
        if (source.SettlementRaceValue == OverworldPOI.SettlementRace.Robber) entityType = OverworldEntity.EntityType.RobberParty;
        if (source.SettlementRaceValue == OverworldPOI.SettlementRace.Pirate) entityType = OverworldEntity.EntityType.PirateCrew;

        var entity = new OverworldEntity
        {
            EntityName = source.GetSettlementRaceName() + (entityType == OverworldEntity.EntityType.RaidingParty ? "掠夺队" : ""),
            EntityTypeEnum = entityType,
            Position = source.Position + new Vector2((float)(_random.NextDouble() * 60 - 30), (float)(_random.NextDouble() * 60 - 30)),
            HomePosition = source.Position,
            TargetPosition = closestVillage.Position,
            SourceSettlement = source,
            PartySize = 4 + _random.Next(8),
            PartyLevel = 1 + (int)(source.ThreatLevel * 2)
        };
        entity.CombatPower = entity.PartySize * entity.PartyLevel * 1.5f;
        entity.MoveSpeed = 160.0f + (float)_random.NextDouble() * 80.0f;
        entity.VisionRange = 300.0f;
        entity.IsHostileToPlayer = true;
        entity.Faction = "hostile";
        entity.CurrentAIState = OverworldEntity.AIState.MovingToTarget;
        entity.LootCarried = 0;

        // 按实体类型分配策略
        entity.AIStrategy = entityType switch
        {
            OverworldEntity.EntityType.BanditParty =>
                new[] { AIStrategyEnum.Reckless, AIStrategyEnum.Cunning, AIStrategyEnum.Intimidate }[_random.Next(3)],
            OverworldEntity.EntityType.RobberParty =>
                _random.Next(2) == 0 ? AIStrategyEnum.Cunning : AIStrategyEnum.Cautious,
            OverworldEntity.EntityType.PirateCrew =>
                new[] { AIStrategyEnum.Reckless, AIStrategyEnum.Intimidate, AIStrategyEnum.Berserk }[_random.Next(3)],
            _ => // RaidingParty (怪物)
                new[] { AIStrategyEnum.Instinct, AIStrategyEnum.Territorial, AIStrategyEnum.Berserk }[_random.Next(3)],
        };

        return entity;
    }

    // ========================================
    // 商队
    // ========================================

    private void GenerateCaravans(List<OverworldPOI> pois, List<OverworldEntity> templates)
    {
        var towns = pois.Where(p => p.PoiTypeEnum == OverworldPOI.POIType.Town).ToList();
        if (towns.Count >= 2)
            for (int i = 0; i < Math.Min(2 + _random.Next(2), towns.Count - 1); i++)
            {
                var entity = new OverworldEntity
                {
                    EntityName = $"商队{i + 1}",
                    EntityTypeEnum = OverworldEntity.EntityType.Caravan,
                    Position = towns[i].Position,
                    HomePosition = towns[i].Position,
                    OriginTown = towns[i],
                    DestinationTown = towns[(i + 1) % towns.Count],
                };
                entity.TargetPosition = entity.DestinationTown.Position;
                entity.MoveSpeed = 120.0f;
                entity.CombatPower = 5.0f;
                entity.PartySize = 3;
                entity.PartyLevel = 1;
                entity.VisionRange = 200.0f;
                entity.IsHostileToPlayer = false;
                entity.Faction = "merchants";
                entity.TradeGoods = 50 + _random.Next(150);
                entity.CurrentAIState = OverworldEntity.AIState.MovingToTarget;
                entity.AIStrategy = AIStrategyEnum.Cautious;
                templates.Add(entity);
            }
    }

    // ========================================
    // 史诗怪物
    // ========================================

    private void GenerateEpicMonsters(List<OverworldPOI> pois, List<OverworldEntity> templates)
    {
        var dragonLairs = pois.Where(p => p.PoiTypeEnum == OverworldPOI.POIType.Lair && p.LairTypeValue == OverworldPOI.LairType.DragonLair).ToList();
        foreach (var lair in dragonLairs)
        {
            bool isNorth = IsLairNorth(lair);
            var entity = new OverworldEntity
            {
                EntityName = isNorth ? "霜冠巨龙" : "远古赤龙",
                EntityTypeEnum = OverworldEntity.EntityType.EpicMonster,
                MonsterType = "dragon",
                Position = lair.Position + new Vector2((float)(_random.NextDouble() * 200 - 100), (float)(_random.NextDouble() * 200 - 100)),
                HomePosition = lair.Position,
                TerritoryCenter = lair.Position,
                TerritoryRadius = 400.0f + (float)_random.NextDouble() * 200.0f
            };
            entity.CombatPower = 30.0f + lair.LairLevel * 5.0f;
            entity.PartyLevel = lair.LairLevel;
            entity.MoveSpeed = 250.0f;
            entity.PatrolRadius = entity.TerritoryRadius;
            entity.VisionRange = 500.0f;
            entity.IsHostileToPlayer = true;
            entity.IsAggressive = false;
            entity.Faction = "hostile";
            entity.AIStrategy = AIStrategyEnum.Territorial;
            templates.Add(entity);
        }
    }

    // ========================================
    // 辅助
    // ========================================

    private bool IsLairNorth(OverworldPOI lair)
    {
        float mid = HexGrid != null ? HexGrid.MapPixelHeight / 2.0f : (Registry?.MapHeight ?? 4096) / 2.0f;
        return lair.Position.Y < mid;
    }
}
