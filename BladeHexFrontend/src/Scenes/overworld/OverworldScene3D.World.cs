// OverworldScene3D.World.cs
// 世界生成 + ChunkManager 流式加载
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.Strategic;
using BladeHex.Data;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    // ========================================
    // Chunk 模式字段
    // ========================================
    private ChunkManager? _chunkManager;
    private string? _chunkSaveId;
    private int _worldSeed;

    /// <summary>所有世界 POI</summary>
    public List<OverworldPOI> WorldPois { get; set; } = new();

    /// <summary>特殊角色（领主 + 冒险者）— 世界生成时创建，InitEntities 时加载到 DormantPool</summary>
    private List<OverworldEntity> _worldSpecialCharacters = new();

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

            var creator = new WorldCreator();
            creator.OnProgress = (progress, msg) =>
            {
                GD.Print($"[WorldCreator] {progress:P0} {msg}");
            };

            var worldData = creator.CreateWorld(seed, config);

            // ChunkManager — 用种子和世界尺寸初始化（含模板网格尺寸）
            _chunkManager = new ChunkManager();
            var (gridW, gridH) = WorldCreationConfig.GetTemplateGrid(worldSize);
            _chunkManager.Initialize(seed, config.WorldTileWidth, config.WorldTileHeight, gridW, gridH);
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

            // 保存 ID
            _chunkSaveId = gs?.Save.CurrentSaveId ?? $"world_{seed}";

            GD.Print($"[OverworldScene3D] WorldCreator 完成: {config.WorldTileWidth}×{config.WorldTileHeight}, POI={WorldPois.Count}");
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"[OverworldScene3D] WorldCreator 失败: {e.Message}");
            GD.PrintErr($"  回退到简单生成");
            FallbackSimpleGeneration(seed);
        }
    }

    /// <summary>简单生成回退</summary>
    private void FallbackSimpleGeneration(int seed)
    {
        _gen = new HexOverworldGenerator();
        _grid = _gen.Generate(64, 48, seed);
        _astar = new HexOverworldAStar { Grid = _grid };
        _chunkManager = null;
    }

    // ========================================
    // Chunk 流式加载
    // ========================================

    /// <summary>每帧更新 chunk 加载</summary>
    private void UpdateChunkLoading()
    {
        if (_chunkManager == null) return;

        var playerAxial = HexOverworldTile.PixelToAxial(_playerPixelPos.X, _playerPixelPos.Y);
        var newChunks = _chunkManager.UpdateChunks(playerAxial.X, playerAxial.Y);

        if (newChunks.Count > 0)
        {
            foreach (var chunk in newChunks)
            {
                _renderer.LoadTiles(chunk.Tiles.Values);
                _propRenderer?.LoadPropsForTiles(chunk.Tiles.Values);

                // 动态追加道路
                OnNewChunkRoads(chunk, chunk.ChunkCoord);

                // 动态扩展 navmesh
                OnNewChunkNavigation(chunk);
            }
        }
    }

    /// <summary>初始加载</summary>
    private void InitialChunkLoad()
    {
        if (_chunkManager == null)
        {
            // 非 chunk 模式：只渲染已揭示的 tile
            if (_fog != null)
            {
                var revealedTiles = new List<HexOverworldTile>();
                foreach (var tile in _grid.Tiles.Values)
                {
                    if (_fog.IsRevealed(tile.PixelPos.X, tile.PixelPos.Y))
                        revealedTiles.Add(tile);
                }
                _renderer.LoadTiles(revealedTiles);
            }
            else
            {
                _renderer.LoadFromGrid(_grid);
            }
            return;
        }

        var playerAxial = HexOverworldTile.PixelToAxial(_playerPixelPos.X, _playerPixelPos.Y);
        var initialChunks = _chunkManager.UpdateChunks(playerAxial.X, playerAxial.Y);

        foreach (var chunk in initialChunks)
            _renderer.LoadTiles(chunk.Tiles.Values);
        foreach (var kvp in _chunkManager.ActiveChunks)
            _renderer.LoadTiles(kvp.Value.Tiles.Values);

        GD.Print($"[OverworldScene3D] 初始 chunk: {_chunkManager.ActiveChunks.Count}");
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

        GD.Print($"[OverworldScene3D] 已保存: {saved} chunks, {WorldPois.Count} POIs");
    }
}
