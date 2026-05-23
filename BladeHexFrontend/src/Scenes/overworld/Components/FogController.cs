// FogController.cs
// 大地图迷雾系统控制器 — 统管 FogOfWar / FogOverlay3D / FogIllustrationLayer
// + CloudLayer3D / WindSystem / TerritoryOverlay 全套天空与战争迷雾子系统。
//
// 重构于 Sprint 6（架构优化 spec R5）：从 OverworldScene3D.Fog partial 抽出。
//
// 公共 API 设计：
//   - Fog/CloudLayer/WindSystem/TerritoryOverlay 等 getter 暴露给主场景调试命令、
//     POIController/MinimapController/WeatherController 等组件注入
//   - IsRevealed / UpdateVision / RenderAllRevealedTiles 是核心使用入口
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.View.Environment;
using BladeHex.Strategic;
using BladeHex.Data;

namespace BladeHex.Scenes.Overworld.Components;

[GlobalClass]
public partial class FogController : Node
{
    // ========================================
    // 注入依赖
    // ========================================

    private HexOverworldGrid? _grid;
    private ChunkManager? _chunkManager;
    private HexOverworldRenderer3D? _renderer;
    private OverworldPropRenderer? _propRenderer;
    private OverworldTerrainSpriteRenderer? _terrainSpriteRenderer;
    private List<OverworldPOI>? _worldPois;
    private Dictionary<string, NationTerritory>? _worldTerritories;
    private List<NationConfig>? _worldNations;
    private Node3D? _scenePathRoot;

    // ========================================
    // 自有
    // ========================================

    private FogOfWar? _fog;
    private FogOverlay3D? _fogOverlay;
    private FogIllustrationLayer? _fogIllustrations;
    private CloudLayer3D? _cloudLayer;
    private WindSystem? _windSystem;
    private TerritoryOverlay? _territoryOverlay;

    /// <summary>已送入 3D 渲染器的 tile 坐标集合</summary>
    private readonly HashSet<Vector2I> _renderedTileCoords = new();

    // ========================================
    // 公共暴露（供其它组件 / 主场景调试 / Weather 等读取）
    // ========================================

    public FogOfWar? Fog => _fog;
    public CloudLayer3D? CloudLayer => _cloudLayer;
    public WindSystem? WindSystem => _windSystem;
    public TerritoryOverlay? TerritoryOverlay => _territoryOverlay;
    public int RenderedTileCount => _renderedTileCoords.Count;

    public bool DisableFog
    {
        get => _fog?.DisableFog ?? false;
        set { if (_fog != null) _fog.DisableFog = value; }
    }

    // ========================================
    // 入口
    // ========================================

    public void Initialize(
        HexOverworldGrid grid,
        ChunkManager? chunkManager,
        HexOverworldRenderer3D renderer,
        OverworldPropRenderer? propRenderer,
        OverworldTerrainSpriteRenderer? terrainSpriteRenderer,
        List<OverworldPOI> worldPois,
        Dictionary<string, NationTerritory>? territories,
        List<NationConfig>? nations,
        Node3D scenePathRoot,
        int playerRaceId,
        Vector2 playerPixelPos)
    {
        _grid = grid;
        _chunkManager = chunkManager;
        _renderer = renderer;
        _propRenderer = propRenderer;
        _terrainSpriteRenderer = terrainSpriteRenderer;
        _worldPois = worldPois;
        _worldTerritories = territories;
        _worldNations = nations;
        _scenePathRoot = scenePathRoot;

        InitFog(playerRaceId, playerPixelPos);
    }

    /// <summary>初始化国家领土覆盖层（必须在 InitFog 之后调用）</summary>
    public void InitTerritoryOverlay()
    {
        if (_worldTerritories == null || _worldNations == null || _fog == null || _scenePathRoot == null) return;

        _territoryOverlay = new TerritoryOverlay { Name = "TerritoryOverlay" };
        AddChild(_territoryOverlay);
        _territoryOverlay.Initialize(_worldTerritories, _worldNations, _fog.MapWidthPx, _fog.MapHeightPx, _scenePathRoot);

        GD.Print("[FogController] 领土覆盖层初始化完成");
    }

    /// <summary>每帧更新迷雾视野 + 增量揭示 tile 到渲染器</summary>
    public void UpdateVision(Vector2 playerPixelPos, float weatherVisionFactor)
    {
        if (_fog == null) return;

        // 设置视野范围为 15 格 hex 半径（像素），天气会缩减视野
        float baseVision = 15.0f * HexOverworldTile.HexSize * 1.732f; // ≈ 4050px
        _fog.VisionRange = baseVision * weatherVisionFactor;

        // 更新视野（玩家周围变为 InVision，远处变为 Revealed）
        _fog.UpdateVision(playerPixelPos);

        // 标记迷雾覆盖层需要刷新
        _fogOverlay?.MarkDirty();

        // 增量揭示新 tile 到 3D 渲染器
        RevealNewTilesToRenderer(playerPixelPos);
    }

    /// <summary>检查位置是否已揭示</summary>
    public bool IsRevealed(float px, float py)
    {
        return _fog == null || _fog.IsRevealed(px, py);
    }

    /// <summary>初始化时渲染所有已揭示的 tile（领土 + 玩家视野）</summary>
    public void RenderAllRevealedTiles()
    {
        if (_fog == null || _chunkManager == null || _renderer == null) return;

        var startTime = Time.GetTicksMsec();
        var tilesToRender = new List<HexOverworldTile>();

        // 遍历所有活跃 chunk 中的 tile
        foreach (var kvp in _chunkManager.ActiveChunks)
        {
            foreach (var tile in kvp.Value.Tiles.Values)
            {
                if (_renderedTileCoords.Contains(tile.Coord)) continue;
                if (!_fog.IsRevealed(tile.PixelPos.X, tile.PixelPos.Y)) continue;

                tilesToRender.Add(tile);
                _renderedTileCoords.Add(tile.Coord);
            }
        }

        // 也检查内存缓存中的 chunk（领土可能跨越未激活的 chunk）
        var generator = _chunkManager.Generator;
        if (generator != null)
        {
            int chunksW = generator.WorldWidth / ChunkData.ChunkSize;
            int chunksH = generator.WorldHeight / ChunkData.ChunkSize;

            for (int cq = 0; cq < chunksW; cq++)
            {
                for (int cr = 0; cr < chunksH; cr++)
                {
                    var coord = new Vector2I(cq, cr);
                    if (_chunkManager.ActiveChunks.ContainsKey(coord)) continue;

                    if (_chunkManager.TryGetFromCache(coord, out var chunk))
                    {
                        foreach (var tile in chunk.Tiles.Values)
                        {
                            if (_renderedTileCoords.Contains(tile.Coord)) continue;
                            if (!_fog.IsRevealed(tile.PixelPos.X, tile.PixelPos.Y)) continue;

                            tilesToRender.Add(tile);
                            _renderedTileCoords.Add(tile.Coord);
                        }
                    }
                }
            }
        }

        if (tilesToRender.Count > 0)
        {
            _renderer.LoadTiles(tilesToRender);
            _propRenderer?.LoadPropsForTiles(tilesToRender);
            _terrainSpriteRenderer?.LoadSpritesForTiles(tilesToRender);
        }

        var elapsed = Time.GetTicksMsec() - startTime;
        GD.Print($"[FogController] 领土预渲染: {tilesToRender.Count} tiles, {elapsed}ms");
    }

    /// <summary>调试用：揭示全图</summary>
    public void RevealAll()
    {
        _fog?.RevealAll();
        RenderAllRevealedTiles();
        _fogOverlay?.FullUpdateFogMask();
    }

    /// <summary>调试用：禁用/启用迷雾切换</summary>
    public void ToggleDisableFog(Vector2 playerPixelPos)
    {
        if (_fog == null) return;

        _fog.DisableFog = !_fog.DisableFog;
        if (_fog.DisableFog)
        {
            _fog.UpdateVision(playerPixelPos);
            RenderAllRevealedTiles();
        }
        _fogOverlay?.FullUpdateFogMask();
    }

    /// <summary>调试用：全量刷新 fog mask shader</summary>
    public void FullUpdateFogMask() => _fogOverlay?.FullUpdateFogMask();

    // ========================================
    // 内部
    // ========================================

    /// <summary>初始化 FogOfWar 实例 + 揭示出身领土 + 初始化覆盖层</summary>
    private void InitFog(int playerRaceId, Vector2 playerPixelPos)
    {
        int mapWPx, mapHPx, cellSz;

        if (_chunkManager?.Generator != null)
        {
            int tileW = _chunkManager.Generator.WorldWidth;
            int tileH = _chunkManager.Generator.WorldHeight;

            var p00 = HexOverworldTile.AxialToPixel(0, 0);
            var pW0 = HexOverworldTile.AxialToPixel(tileW, 0);
            var p0H = HexOverworldTile.AxialToPixel(0, tileH);
            var pWH = HexOverworldTile.AxialToPixel(tileW, tileH);

            float minX = Mathf.Min(Mathf.Min(p00.X, pW0.X), Mathf.Min(p0H.X, pWH.X));
            float maxX = Mathf.Max(Mathf.Max(p00.X, pW0.X), Mathf.Max(p0H.X, pWH.X));
            float minY = Mathf.Min(Mathf.Min(p00.Y, pW0.Y), Mathf.Min(p0H.Y, pWH.Y));
            float maxY = Mathf.Max(Mathf.Max(p00.Y, pW0.Y), Mathf.Max(p0H.Y, pWH.Y));

            mapWPx = (int)(maxX - minX) + 2000;
            mapHPx = (int)(maxY - minY) + 2000;
            cellSz = 128;
        }
        else
        {
            mapWPx = (int)(_grid?.MapPixelWidth ?? 0);
            mapHPx = (int)(_grid?.MapPixelHeight ?? 0);
            cellSz = 312; // 2 * HexSize
        }

        _fog = new FogOfWar();
        _fog.Initialize(mapWPx, mapHPx, cellSz);

        // 揭示出身国家领土
        RevealHomeTerritory(playerRaceId);

        // 揭示玩家周围（15 格半径）
        _fog.VisionRange = 15.0f * HexOverworldTile.HexSize * 1.732f;
        _fog.UpdateVision(playerPixelPos);

        GD.Print($"[FogController] 迷雾: {_fog.GridW}×{_fog.GridH} grid, cell={cellSz}px");

        // 初始化迷雾纹理覆盖层（羊皮纸风格）
        InitFogOverlay(mapWPx, mapHPx);

        // 初始化云层
        InitCloudLayer(mapWPx, mapHPx);
    }

    private void InitFogOverlay(int mapWPx, int mapHPx)
    {
        if (_fog == null) return;

        _fogOverlay = new FogOverlay3D { Name = "FogOverlay3D" };
        AddChild(_fogOverlay);
        _fogOverlay.Initialize(_fog, mapWPx, mapHPx);

        // 初始化插画层（共享 fog mask 纹理，实现逐像素裁剪）
        var fogMask = _fogOverlay.GetFogMaskTexture();
        if (fogMask != null)
        {
            _fogIllustrations = new FogIllustrationLayer { Name = "FogIllustrationLayer" };
            AddChild(_fogIllustrations);
            _fogIllustrations.Initialize(_fog, fogMask, mapWPx, mapHPx);

            GenerateFogIllustrations();
        }

        GD.Print("[FogController] 迷雾纹理覆盖层 + 插画层初始化完成");
    }

    private void InitCloudLayer(int mapWPx, int mapHPx)
    {
        // 风力系统
        _windSystem = new WindSystem
        {
            Name = "WindSystem",
            BaseWindStrength = 0.4f,
            BaseWindAngle = 0.3f,
        };
        AddChild(_windSystem);

        // 云层（单个粒子系统，风向由 WindSystem 驱动）
        _cloudLayer = new CloudLayer3D
        {
            Name = "CloudLayer3D",
            CloudCoverage = 0.45f,
            CloudOpacity = 0.35f,
        };
        AddChild(_cloudLayer);
        _cloudLayer.Initialize(mapWPx, mapHPx);

        // 连接风力系统到云层
        _windSystem.RegisterCloudLayer(_cloudLayer);

        GD.Print("[FogController] 风力系统 + 云层初始化完成");
    }

    private void RevealHomeTerritory(int playerRaceId)
    {
        if (_fog == null) return;

        var playerRace = (RaceData.Race)playerRaceId;

        // 有实际领土数据 → 精确揭示
        if (_worldTerritories != null && _worldNations != null)
        {
            var homeNation = RaceNationMapping.FindHomeNation(playerRace, _worldTerritories, _worldNations);
            if (homeNation != null && _worldTerritories.TryGetValue(homeNation.Id, out var territory))
            {
                _fog.RevealTerritory(territory.AllTiles);
                GD.Print($"[FogController] 母国领土揭示: {homeNation.DisplayName}, {territory.TotalTiles} tiles");
                return;
            }
        }

        // 无领土数据 → fallback
        _fog.RevealRaceRegionFallback(playerRace);
        GD.Print($"[FogController] 母国领土揭示: fallback (race={playerRace})");
    }

    /// <summary>将新揭示的 tile 增量加载到 3D 渲染器</summary>
    private void RevealNewTilesToRenderer(Vector2 playerPixelPos)
    {
        if (_fog == null || _renderer == null) return;

        var playerAxial = HexOverworldTile.PixelToAxial(playerPixelPos.X, playerPixelPos.Y);
        int checkRadius = 18; // 略大于视野半径，确保边缘 tile 也被渲染

        var newTiles = new List<HexOverworldTile>();

        for (int dq = -checkRadius; dq <= checkRadius; dq++)
        {
            int r1 = Mathf.Max(-checkRadius, -dq - checkRadius);
            int r2 = Mathf.Min(checkRadius, -dq + checkRadius);
            for (int dr = r1; dr <= r2; dr++)
            {
                int q = playerAxial.X + dq;
                int r = playerAxial.Y + dr;
                var coord = new Vector2I(q, r);

                if (_renderedTileCoords.Contains(coord)) continue;

                HexOverworldTile? tile = null;
                if (_chunkManager != null)
                    tile = _chunkManager.GetTile(q, r);
                else
                    tile = _grid?.GetTile(q, r);

                if (tile == null) continue;
                if (!_fog.IsRevealed(tile.PixelPos.X, tile.PixelPos.Y)) continue;

                newTiles.Add(tile);
                _renderedTileCoords.Add(coord);
            }
        }

        if (newTiles.Count > 0)
        {
            _renderer.LoadTiles(newTiles);
            _propRenderer?.LoadPropsForTiles(newTiles);
            _terrainSpriteRenderer?.LoadSpritesForTiles(newTiles);
        }
    }

    /// <summary>
    /// 根据世界 POI 和地形特征生成迷雾插画。
    /// 在危险区域/特殊地标附近放置对应主题的抽象绘画。
    /// </summary>
    private void GenerateFogIllustrations()
    {
        if (_fogIllustrations == null || _worldPois == null) return;

        var illustrations = new List<FogIllustration>();
        int id = 0;

        // 1. 基于 POI 类型放置插画
        foreach (var poi in _worldPois)
        {
            IllustrationType? type = null;

            if (poi.PoiTypeEnum == OverworldPOI.POIType.Lair)
            {
                type = poi.LairTypeValue switch
                {
                    OverworldPOI.LairType.DragonLair => IllustrationType.Dragon,
                    OverworldPOI.LairType.AncientTomb => IllustrationType.Skull,
                    OverworldPOI.LairType.Ruins => IllustrationType.Skull,
                    OverworldPOI.LairType.GolemForge => IllustrationType.Giant,
                    OverworldPOI.LairType.BanditCamp => IllustrationType.Wolf,
                    OverworldPOI.LairType.PirateCove => IllustrationType.Ship,
                    _ => IllustrationType.Skull,
                };
            }
            else if (poi.PoiTypeEnum == OverworldPOI.POIType.Settlement)
            {
                type = poi.SettlementRaceValue switch
                {
                    OverworldPOI.SettlementRace.Goblin => IllustrationType.Wolf,
                    OverworldPOI.SettlementRace.Minotaur => IllustrationType.Giant,
                    OverworldPOI.SettlementRace.ShadowCult => IllustrationType.Flame,
                    _ => IllustrationType.Serpent,
                };
            }
            else if (poi.PoiTypeEnum == OverworldPOI.POIType.Shrine)
            {
                type = IllustrationType.Treant;
            }
            else if (poi.PoiTypeEnum == OverworldPOI.POIType.Castle)
            {
                type = IllustrationType.Eagle;
            }

            if (type == null) continue;

            float offsetX = ((id * 137) % 200 - 100) * 2.0f;
            float offsetY = ((id * 251) % 200 - 100) * 2.0f;

            illustrations.Add(new FogIllustration
            {
                Id = $"poi_{id++}",
                Type = type.Value,
                WorldPosition = poi.Position + new Vector2(offsetX, offsetY),
                Size = 6.0f + (id % 3) * 1.5f,
                Rotation = (id * 0.7f) % (Mathf.Pi * 0.4f) - Mathf.Pi * 0.2f,
                Tint = new Color(0.4f, 0.3f, 0.22f, 0.65f),
                LinkedRegion = poi.PoiName,
            });
        }

        // 2. 在大片未探索水域放置海怪
        if (_chunkManager != null)
        {
            var rng = new System.Random((_chunkManager.Generator?.WorldSeed ?? 0) ^ 0x4D4F4E53);
            int seaMonsters = 2 + rng.Next(3);
            for (int i = 0; i < seaMonsters; i++)
            {
                float x = rng.Next(100, (int)(_fog?.MapWidthPx ?? 10000) - 100);
                float y = rng.Next(100, (int)(_fog?.MapHeightPx ?? 8000) - 100);

                var axial = HexOverworldTile.PixelToAxial(x, y);
                var tile = _chunkManager.GetTile(axial.X, axial.Y);
                if (tile == null || (tile.Terrain != HexOverworldTile.TerrainType.DeepWater &&
                                     tile.Terrain != HexOverworldTile.TerrainType.ShallowWater))
                    continue;

                illustrations.Add(new FogIllustration
                {
                    Id = $"sea_{id++}",
                    Type = IllustrationType.SeaMonster,
                    WorldPosition = new Vector2(x, y),
                    Size = 8.0f + rng.Next(4),
                    Rotation = (float)(rng.NextDouble() * Mathf.Pi * 0.5f - Mathf.Pi * 0.25f),
                    Tint = new Color(0.3f, 0.35f, 0.4f, 0.55f),
                    LinkedRegion = "深海",
                });
            }
        }

        // 3. 在地图角落放置装饰性指南针
        if (_fog != null)
        {
            float mapW = _fog.MapWidthPx;
            float mapH = _fog.MapHeightPx;
            illustrations.Add(new FogIllustration
            {
                Id = $"compass_{id++}",
                Type = IllustrationType.Compass,
                WorldPosition = new Vector2(mapW * 0.85f, mapH * 0.15f),
                Size = 10.0f,
                Rotation = 0.0f,
                Tint = new Color(0.5f, 0.4f, 0.3f, 0.5f),
                LinkedRegion = "装饰",
            });
        }

        _fogIllustrations.AddIllustrations(illustrations);
    }
}
