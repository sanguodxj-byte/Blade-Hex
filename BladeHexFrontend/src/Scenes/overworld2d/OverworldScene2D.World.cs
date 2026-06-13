// OverworldScene2D.World.cs
// 世界生成 + ChunkManager 流式加载 — 从 OverworldScene3D.World.cs 迁移
// 纯逻辑层，无 3D 依赖
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.Data;
using BladeHex.Diagnostics;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // ========================================
    // Chunk 模式字段
    // ========================================
    private ChunkManager? _chunkManager;
    private string? _chunkSaveId;
    private int _worldSeed;

    /// <summary>所有世界 POI</summary>
    private List<OverworldPOI> _worldPoisBacking = new();
    public List<OverworldPOI> WorldPois
    {
        get => _worldPoisBacking;
        set
        {
            _worldPoisBacking = value;
            if (_worldPoisBacking != null)
                OverworldPOI.BindParentChildRelationships(_worldPoisBacking);
        }
    }

    /// <summary>特殊角色（领主 + 冒险者）— 世界生成时创建，InitEntities 时加载到 DormantPool</summary>
    private List<OverworldEntity> _worldSpecialCharacters = new();

    // ========================================
    // 生态区命名系统
    // ========================================
    private List<NamedBiomeZone>? _namedBiomeZones;
    private BiomeZoneNamer? _biomeZoneNamer;
    private BladeHex.View.UI.Overworld.RegionNameOverlay? _regionNameOverlay;

    // ========================================
    // 世界生成
    // ========================================

    /// <summary>
    /// 生成世界。尝试 WorldCreator，失败则回退简单生成。
    /// </summary>
    private void InitWorldGeneration(int seed)
    {
        try
        {
            var gs = BladeHex.Data.Globals.StateOrNull;
            var worldSize = gs != null ? (WorldCreationConfig.WorldSize)gs.WorldGen.Size : WorldCreationConfig.WorldSize.Small;
            var config = WorldCreationConfig.Create(worldSize, seed);
            DiagnosticLog.Event("OverworldScene2D", "world_config", new Dictionary<string, object?>
            {
                ["seed"] = seed,
                ["world_size"] = worldSize,
                ["chunks"] = $"{config.WorldChunksW}x{config.WorldChunksH}",
                ["tiles"] = $"{config.WorldTileWidth}x{config.WorldTileHeight}",
                ["nations"] = config.Nations.Count,
            });

            var creator = new WorldCreator();
            creator.OnProgress = (progress, msg) =>
            {
                GD.Print($"[WorldCreator] {progress:P0} {msg}");
                DiagnosticLog.Event("WorldCreator", "progress", new Dictionary<string, object?>
                {
                    ["progress"] = progress.ToString("P0"),
                    ["message"] = msg,
                });
            };

            var worldData = creator.CreateWorld(seed, config);
            DiagnosticLog.Event("OverworldScene2D", "world_data_created", new Dictionary<string, object?>
            {
                ["chunks"] = worldData.Chunks.Count,
                ["pois"] = worldData.Pois.Count,
                ["territories"] = worldData.Territories.Count,
                ["nations"] = worldData.Nations.Count,
                ["specials"] = worldData.SpecialCharacters.Count,
            });

            // ChunkManager — 用种子和世界尺寸初始化（含模板网格尺寸）
            _chunkManager = new ChunkManager();
            var (gridW, gridH) = WorldCreationConfig.GetTemplateGrid(worldSize);
            _chunkManager.Initialize(seed, config.WorldTileWidth, config.WorldTileHeight, gridW, gridH);
            ApplyChunkStreamingSettings();
            // 将生成的 chunk 数据加载到内存缓存
            _chunkManager.LoadIntoMemory(worldData.Chunks);

            // 存储世界数据
            WorldPois = worldData.Pois;
            _worldTerritories = worldData.Territories;
            _worldNations = worldData.Nations;
            _worldSpecialCharacters = worldData.SpecialCharacters;
            _worldSeed = seed;

            // HexGrid 兼容
            _grid = new HexOverworldGrid();
            _grid.Initialize(config.WorldTileWidth, config.WorldTileHeight);
            _astar = new HexOverworldAStar { Grid = _grid };
            _mapAccess = new BladeHex.View.Map.OverworldMapAccess(_chunkManager, _grid);

            // 保存 ID
            _chunkSaveId = gs?.Save.CurrentSaveId ?? $"world_{seed}";

            // 生态区分析 + 命名
            RunBiomeZoneAnalysis(seed);

            GD.Print($"[OverworldScene2D] WorldCreator 完成: {config.WorldTileWidth}×{config.WorldTileHeight}, POI={WorldPois.Count}");
            DiagnosticLog.Event("OverworldScene2D", "world_creator_complete", new Dictionary<string, object?>
            {
                ["tiles"] = $"{config.WorldTileWidth}x{config.WorldTileHeight}",
                ["pois"] = WorldPois.Count,
            });
        }
        catch (System.Exception e)
        {
            DiagnosticLog.Exception("OverworldScene2D.InitWorldGeneration", e);
            GD.PrintErr($"[OverworldScene2D] WorldCreator 失败: {e.Message}");
            GD.PrintErr($"  回退到简单生成");
            FallbackSimpleGeneration(seed);
        }
    }

    /// <summary>简单生成回退</summary>
    private void FallbackSimpleGeneration(int seed)
    {
        DiagnosticLog.Event("OverworldScene2D", "fallback_simple_generation", new Dictionary<string, object?> { ["seed"] = seed });
        _gen = new HexOverworldGenerator();
        _grid = _gen.Generate(64, 48, seed);
        _astar = new HexOverworldAStar { Grid = _grid };
        _chunkManager = null;
        _mapAccess = new BladeHex.View.Map.OverworldMapAccess(_chunkManager, _grid);
    }

    // ========================================
    // 生态区分析 + 命名
    // ========================================

    /// <summary>执行生态区聚类分析并为每个区域生成名称</summary>
    private void RunBiomeZoneAnalysis(int seed)
    {
        if (_chunkManager == null)
        {
            GD.Print("[OverworldScene2D] 跳过生态区分析: 非 Chunk 模式");
            return;
        }

        var startTime = Time.GetTicksMsec();

        // 1. 执行 flood-fill 聚类
        var analyzer = new BiomeZoneAnalyzer();
        analyzer.MinZoneSize = 200; // 最小 200 tiles 才算独立生态区

        var allChunks = _chunkManager.AllKnownChunks;
        var zones = analyzer.Analyze(allChunks);

        // 2. 为每个生态区生成名称
        _biomeZoneNamer = new BiomeZoneNamer(seed);
        _namedBiomeZones = _biomeZoneNamer.NameAllZones(zones);

        var elapsed = Time.GetTicksMsec() - startTime;
        GD.Print($"[OverworldScene2D] 生态区分析完成: {_namedBiomeZones.Count} 个区域, 耗时 {elapsed}ms");
    }

    // ========================================
    // Chunk 流式加载
    // ========================================

    /// <summary>每帧更新 chunk 加载</summary>
    private void UpdateChunkLoading()
    {
        if (_chunkManager == null) return;
        if (!_chunkStreamingEnabled) return;

        var playerAxial = HexOverworldTile.PixelToAxial(_playerPixelPos.X, _playerPixelPos.Y);
        if (ChunkData.WorldToChunk(playerAxial.X, playerAxial.Y) == _chunkManager.PlayerChunk)
            return;

        var previousActiveChunks = new Dictionary<Vector2I, ChunkData>(_chunkManager.ActiveChunks);
        var newChunks = _chunkManager.UpdateChunks(playerAxial.X, playerAxial.Y);
        bool unloadedChunks = UnloadInactiveChunkDecorations(previousActiveChunks, out var unloadedTileCoords);

        if (newChunks.Count > 0)
        {
            ProcessActivatedEncounterChunks(newChunks);
            LoadGroundCacheTiles(EnumerateChunkTiles(newChunks));
            LoadStreamingDecorationTiles(newChunks);
        }

        if (newChunks.Count > 0 || unloadedChunks)
        {
            ReloadPropBoundaryTiles(newChunks, unloadedTileCoords);
            OnActiveChunksRoadsChanged(newChunks);
        }
    }

    private void ApplyChunkStreamingSettings()
    {
        if (_chunkManager == null)
            return;

        _chunkManager.LoadRadius = _chunkLoadRadius;
        _chunkManager.UnloadRadius = _chunkUnloadRadius;
    }

    private bool UnloadInactiveChunkDecorations(
        Dictionary<Vector2I, ChunkData> previousActiveChunks,
        out HashSet<Vector2I> unloadedTileCoords)
    {
        unloadedTileCoords = new HashSet<Vector2I>();
        if (_chunkManager == null || previousActiveChunks.Count == 0)
            return false;

        foreach (var kvp in previousActiveChunks)
        {
            if (_chunkManager.ActiveChunks.ContainsKey(kvp.Key))
                continue;

            foreach (var tileCoord in kvp.Value.Tiles.Keys)
            {
                unloadedTileCoords.Add(tileCoord);
                _streamedDecorationTileCoords.Remove(tileCoord);
            }
        }

        if (unloadedTileCoords.Count == 0)
            return false;

        _propRenderer?.UnloadTiles(unloadedTileCoords);
        _decalRenderer?.UnloadTiles(unloadedTileCoords);
        return true;
    }

    private void ReloadPropBoundaryTiles(List<ChunkData> newChunks, HashSet<Vector2I> unloadedTileCoords)
    {
        if (_chunkManager == null || _propRenderer == null)
            return;

        var changedCoords = new HashSet<Vector2I>(unloadedTileCoords);
        foreach (var chunk in newChunks)
        {
            foreach (var coord in chunk.Tiles.Keys)
                changedCoords.Add(coord);
        }

        if (changedCoords.Count == 0)
            return;

        var reloadCoords = new HashSet<Vector2I>();
        foreach (var coord in changedCoords)
        {
            for (int dir = 0; dir < 6; dir++)
            {
                var neighbor = HexOverworldTile.GetNeighbor(coord.X, coord.Y, dir);
                if (!_streamedDecorationTileCoords.Contains(neighbor))
                    continue;
                if (!_chunkManager.IsLoaded(neighbor.X, neighbor.Y))
                    continue;

                reloadCoords.Add(neighbor);
            }
        }

        if (reloadCoords.Count == 0)
            return;

        var reloadTiles = new List<HexOverworldTile>();
        foreach (var coord in reloadCoords)
        {
            var tile = _chunkManager.GetTile(coord.X, coord.Y);
            if (tile != null)
                reloadTiles.Add(tile);
        }

        _propRenderer.ReloadTiles(reloadTiles);
    }

    /// <summary>初始加载</summary>
    private void InitialChunkLoad()
    {
        if (_chunkManager == null)
        {
            // 非 chunk 模式：地面全图缓存，装饰层按迷雾/揭示状态加载。
            LoadGroundCacheTiles(_grid.Tiles.Values);

            if (_fog != null)
            {
                LoadRevealedStreamingDecorationTiles(_grid.Tiles.Values);
            }
            else
            {
                LoadStreamingDecorationTiles(_grid.Tiles.Values);
            }
            return;
        }

        var playerAxial = HexOverworldTile.PixelToAxial(_playerPixelPos.X, _playerPixelPos.Y);
        _chunkManager.UpdateChunks(playerAxial.X, playerAxial.Y);

        LoadGroundCacheTiles(EnumerateKnownChunkTiles());
        LoadStreamingDecorationTiles(_mapAccess.ActiveChunks.Values);

        GD.Print($"[OverworldScene2D] 初始 chunk: {_chunkManager.ActiveChunks.Count}, prop总数: {_propRenderer?.PropCount ?? 0}");
    }

    private IEnumerable<HexOverworldTile> EnumerateKnownChunkTiles()
    {
        if (_chunkManager == null)
            yield break;

        foreach (var chunk in _chunkManager.AllKnownChunks.Values)
        {
            foreach (var tile in chunk.Tiles.Values)
                yield return tile;
        }
    }

    private IEnumerable<HexOverworldTile> EnumerateChunkTiles(IEnumerable<ChunkData> chunks)
    {
        foreach (var chunk in chunks)
        {
            foreach (var tile in chunk.Tiles.Values)
                yield return tile;
        }
    }

    private void LoadGroundCacheTiles(IEnumerable<HexOverworldTile> tiles)
    {
        _renderer.LoadTiles(tiles);
    }

    private void LoadStreamingDecorationTiles(IEnumerable<HexOverworldTile> tiles)
    {
        BeginStreamingDecorationBatch();
        foreach (var tile in tiles)
            AddStreamingDecorationTile(tile);

        FlushStreamingDecorationBatch();
    }

    private void LoadRevealedStreamingDecorationTiles(IEnumerable<HexOverworldTile> tiles)
    {
        if (_fog == null)
            return;

        BeginStreamingDecorationBatch();
        foreach (var tile in tiles)
        {
            if (!_fog.IsRevealed(tile.PixelPos.X, tile.PixelPos.Y))
                continue;

            AddStreamingDecorationTile(tile);
        }

        FlushStreamingDecorationBatch();
    }

    private void LoadStreamingDecorationTiles(IEnumerable<ChunkData> chunks)
    {
        BeginStreamingDecorationBatch();
        foreach (var chunk in chunks)
        {
            foreach (var tile in chunk.Tiles.Values)
                AddStreamingDecorationTile(tile);
        }

        FlushStreamingDecorationBatch();
    }

    private void BeginStreamingDecorationBatch()
    {
        _streamingDecorationLoadBuffer.Clear();
    }

    private void AddStreamingDecorationTile(HexOverworldTile tile)
    {
        if (!_streamedDecorationTileCoords.Add(tile.Coord))
            return;

        _streamingDecorationLoadBuffer.Add(tile);
    }

    private void FlushStreamingDecorationBatch()
    {
        if (_streamingDecorationLoadBuffer.Count == 0)
            return;

        _propRenderer?.LoadPropsForTiles(_streamingDecorationLoadBuffer);
        _decalRenderer?.LoadDecalsForTiles(_streamingDecorationLoadBuffer);
        _streamingDecorationLoadBuffer.Clear();
    }

    /// <summary>获取玩家起始位置</summary>
    private Vector2 GetPlayerStartPosition()
    {
        // 根据玩家种族找到对应国家的城镇作为起始点
        string playerFaction = FindPlayerHomeFaction();

        // 优先找本国首都（第一个 Town）
        if (!string.IsNullOrEmpty(playerFaction) && WorldPois.Count > 0)
        {
            foreach (var poi in WorldPois)
            {
                if (poi.OwningFaction == playerFaction && poi.PoiTypeEnum == OverworldPOI.POIType.Town)
                    return poi.Position + new Vector2(200, 0);
            }
            // 没有 Town？找本国任意 POI
            foreach (var poi in WorldPois)
            {
                if (poi.OwningFaction == playerFaction)
                    return poi.Position + new Vector2(100, 0);
            }
        }

        // Fallback：第一个城镇
        if (WorldPois.Count > 0)
        {
            foreach (var poi in WorldPois)
            {
                if (poi.PoiTypeEnum == OverworldPOI.POIType.Town)
                    return poi.Position + new Vector2(200, 0);
            }
            return WorldPois[0].Position + new Vector2(100, 0);
        }

        return _grid.GetValidStartPos();
    }

    /// <summary>根据玩家种族找到所属国家 ID</summary>
    private string FindPlayerHomeFaction()
    {
        if (_worldNations == null || _worldTerritories == null) return "";

        var playerRace = (BladeHex.Data.RaceData.Race)PlayerRaceId;
        var homeNation = RaceNationMapping.FindHomeNation(playerRace, _worldTerritories, _worldNations);
        return homeNation?.Id ?? "";
    }

    // ========================================
    // 持久化
    // ========================================

    /// <summary>保存世界数据：chunk 缓存 + POI 列表落盘。</summary>
    public void SaveWorldData()
    {
        if (_chunkManager == null || string.IsNullOrEmpty(_chunkSaveId)) return;

        int saved = _chunkManager.SaveAllToDisk(_chunkSaveId);
        ChunkPersistence.SavePois(_chunkSaveId, WorldPois);

        GD.Print($"[OverworldScene2D] 已保存: {saved} chunks, {WorldPois.Count} POIs");
    }

    // ========================================
    // 道路回调
    // ========================================

    /// <summary>活跃 chunk 集合变化时更新道路与河流渲染</summary>
    private void OnActiveChunksRoadsChanged(List<ChunkData> newChunks)
    {
        if (newChunks.Count > 0)
            _roadRenderer?.OnNewChunksLoaded(newChunks);
        else
            _roadRenderer?.RebuildFromChunks();
        _riverRenderer?.RebuildFromChunks();
    }

    private void ProcessActivatedEncounterChunks(IEnumerable<ChunkData> chunks)
    {
        if (EntityMgr == null) return;

        int seed = BladeHex.Data.Globals.StateOrNull?.WorldGen.Seed ?? 12345;
        int daysElapsed = CurrentDay;
        Func<ChunkData, float> dangerResolver = chunk =>
            _chunkManager?.Generator?.GetDangerLevel(chunk.ChunkCoord.X, chunk.ChunkCoord.Y) ?? 0.5f;

        var events = EntityMgr.ProcessActivatedEncounterChunks(
            chunks,
            _encounterSpawner,
            dangerResolver,
            daysElapsed,
            seed);

        int spawnedCount = 0;
        var samples = new List<string>();
        foreach (var evt in events)
        {
            if (evt.Type != OverworldSimulationEvent.EventType.EntitySpawned || evt.EntityA == null)
                continue;

            var wildBeast = evt.EntityA;
            spawnedCount++;
            if (samples.Count < 5)
                samples.Add($"{wildBeast.EntityName}/{wildBeast.AIStrategy}/{wildBeast.CombatPower:F1}");
        }

        if (spawnedCount > 0)
        {
            string suffix = spawnedCount > samples.Count ? ", ..." : "";
            GD.Print($"[OverworldScene2D] 生态遭遇孵化: {spawnedCount} wild monsters ({string.Join(", ", samples)}{suffix})");
        }
    }

    private void SpawnWildMonstersForInitialActiveChunks()
    {
        if (_chunkManager == null || EntityMgr == null)
            return;

        if (BladeHex.Data.Globals.StateOrNull?.Save.IsLoadingSave == true)
            return;

        int before = EntityMgr.Entities.Count;
        ProcessActivatedEncounterChunks(_chunkManager.ActiveChunks.Values);

        int spawned = EntityMgr.Entities.Count - before;
        if (spawned > 0)
            GD.Print($"[OverworldScene2D] Initial active chunks spawned {spawned} wild monsters");
    }
}
