// OverworldScene3D.Fog.cs
// 迷雾系统 — 完整实现：领土揭示 + 视野跟随 + 增量渲染 + 纹理覆盖 + 云层
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;
using BladeHex.View.Environment;
using BladeHex.Strategic;
using BladeHex.Data;

namespace BladeHex.Scenes.Overworld;

public partial class OverworldScene3D
{
    // ========================================
    // 迷雾系统
    // ========================================

    private FogOfWar? _fog;
    private FogOverlay3D? _fogOverlay;
    private FogIllustrationLayer? _fogIllustrations;
    private CloudLayer3D? _cloudLayer;
    private WindSystem? _windSystem;
    private TerritoryOverlay? _territoryOverlay;
    private Dictionary<string, NationTerritory>? _worldTerritories;
    private List<NationConfig>? _worldNations;

    /// <summary>已送入 3D 渲染器的 tile 坐标集合</summary>
    private readonly HashSet<Vector2I> _renderedTileCoords = new();

    /// <summary>初始化迷雾系统</summary>
    private void InitFog()
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
            mapWPx = (int)_grid.MapPixelWidth;
            mapHPx = (int)_grid.MapPixelHeight;
            cellSz = 312; // 2 * HexSize
        }

        _fog = new FogOfWar();
        _fog.Initialize(mapWPx, mapHPx, cellSz);

        // 揭示出身国家领土
        RevealHomeTerritory();

        // 揭示玩家周围（15 格半径）
        _fog.VisionRange = 15.0f * HexOverworldTile.HexSize * 1.732f;
        _fog.UpdateVision(_playerPixelPos);

        GD.Print("[OverworldScene3D] 迷雾: {0}×{1} grid, cell={2}px".Replace("{0}", _fog.GridW.ToString()).Replace("{1}", _fog.GridH.ToString()).Replace("{2}", cellSz.ToString()));

        // 初始化迷雾纹理覆盖层（羊皮纸风格）
        InitFogOverlay(mapWPx, mapHPx);

        // 初始化云层
        InitCloudLayer(mapWPx, mapHPx);
    }

    /// <summary>初始化国家领土覆盖层</summary>
    private void InitTerritoryOverlay()
    {
        if (_worldTerritories == null || _worldNations == null || _fog == null) return;

        _territoryOverlay = new TerritoryOverlay();
        _territoryOverlay.Name = "TerritoryOverlay";
        AddChild(_territoryOverlay);
        _territoryOverlay.Initialize(_worldTerritories, _worldNations, _fog.MapWidthPx, _fog.MapHeightPx, this);

        GD.Print($"[OverworldScene3D] 领土覆盖层初始化完成");
    }

    /// <summary>初始化迷雾纹理覆盖层</summary>
    private void InitFogOverlay(int mapWPx, int mapHPx)
    {
        if (_fog == null) return;

        _fogOverlay = new FogOverlay3D();
        _fogOverlay.Name = "FogOverlay3D";
        AddChild(_fogOverlay);
        _fogOverlay.Initialize(_fog, mapWPx, mapHPx);

        // 初始化插画层（共享 fog mask 纹理，实现逐像素裁剪）
        var fogMask = _fogOverlay.GetFogMaskTexture();
        if (fogMask != null)
        {
            _fogIllustrations = new FogIllustrationLayer();
            _fogIllustrations.Name = "FogIllustrationLayer";
            AddChild(_fogIllustrations);
            _fogIllustrations.Initialize(_fog, fogMask, mapWPx, mapHPx);

            // 根据世界数据生成插画
            GenerateFogIllustrations();
        }

        GD.Print("[OverworldScene3D] 迷雾纹理覆盖层 + 插画层初始化完成");
    }

    /// <summary>初始化云层模拟</summary>
    private void InitCloudLayer(int mapWPx, int mapHPx)
    {
        // 风力系统
        _windSystem = new WindSystem();
        _windSystem.Name = "WindSystem";
        _windSystem.BaseWindStrength = 0.4f;
        _windSystem.BaseWindAngle = 0.3f;
        AddChild(_windSystem);

        // 云层（单个粒子系统，风向由 WindSystem 驱动）
        _cloudLayer = new CloudLayer3D();
        _cloudLayer.Name = "CloudLayer3D";
        _cloudLayer.CloudCoverage = 0.45f;
        _cloudLayer.CloudOpacity = 0.35f;
        AddChild(_cloudLayer);
        _cloudLayer.Initialize(mapWPx, mapHPx);

        // 连接风力系统到云层
        _windSystem.RegisterCloudLayer(_cloudLayer);

        GD.Print("[OverworldScene3D] 风力系统 + 云层初始化完成");
    }

    /// <summary>揭示玩家出身种族的母国领土</summary>
    private void RevealHomeTerritory()
    {
        if (_fog == null) return;

        var playerRace = (RaceData.Race)PlayerRaceId;

        // 有实际领土数据 → 精确揭示
        if (_worldTerritories != null && _worldNations != null)
        {
            var homeNation = RaceNationMapping.FindHomeNation(playerRace, _worldTerritories, _worldNations);
            if (homeNation != null && _worldTerritories.TryGetValue(homeNation.Id, out var territory))
            {
                _fog.RevealTerritory(territory.AllTiles);
                GD.Print($"[OverworldScene3D] 母国领土揭示: {homeNation.DisplayName}, {territory.TotalTiles} tiles");
                return;
            }
        }

        // 无领土数据 → fallback
        _fog.RevealRaceRegionFallback(playerRace);
        GD.Print($"[OverworldScene3D] 母国领土揭示: fallback (race={playerRace})");
    }

    /// <summary>每帧更新迷雾视野 + 增量揭示 tile 到渲染器</summary>
    private void UpdateFog()
    {
        if (_fog == null) return;

        // 设置视野范围为 15 格 hex 半径（像素），天气会缩减视野
        float baseVision = 15.0f * HexOverworldTile.HexSize * 1.732f; // ≈ 4050px
        _fog.VisionRange = baseVision * WeatherVisionFactor;

        // 更新视野（玩家周围变为 InVision，远处变为 Revealed）
        _fog.UpdateVision(_playerPixelPos);

        // 标记迷雾覆盖层需要刷新
        _fogOverlay?.MarkDirty();

        // 增量揭示新 tile 到 3D 渲染器
        RevealNewTilesToRenderer();
    }

    /// <summary>将新揭示的 tile 增量加载到 3D 渲染器</summary>
    private void RevealNewTilesToRenderer()
    {
        if (_fog == null) return;

        var playerAxial = HexOverworldTile.PixelToAxial(_playerPixelPos.X, _playerPixelPos.Y);
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
                    tile = _grid.GetTile(q, r);

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

    /// <summary>检查位置是否已揭示</summary>
    private bool IsFogRevealed(float px, float py)
    {
        return _fog == null || _fog.IsRevealed(px, py);
    }

    /// <summary>
    /// 根据世界 POI 和地形特征生成迷雾插画。
    /// 在危险区域/特殊地标附近放置对应主题的抽象绘画。
    /// </summary>
    private void GenerateFogIllustrations()
    {
        if (_fogIllustrations == null) return;

        var illustrations = new List<FogIllustration>();
        int id = 0;

        // 1. 基于 POI 类型放置插画
        foreach (var poi in WorldPois)
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

            // 偏移位置（不要正好在 POI 上，稍微偏移让玩家探索时有惊喜）
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
                // 随机选择地图边缘的水域位置
                float x = rng.Next(100, (int)(_fog?.MapWidthPx ?? 10000) - 100);
                float y = rng.Next(100, (int)(_fog?.MapHeightPx ?? 8000) - 100);

                // 确认是水域
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

    /// <summary>初始化时渲染所有已揭示的 tile（领土 + 玩家视野）</summary>
    private void RenderAllRevealedTiles()
    {
        if (_fog == null || _chunkManager == null) return;

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
        GD.Print($"[OverworldScene3D] 领土预渲染: {tilesToRender.Count} tiles, {elapsed}ms");
    }

    // ========================================
    // 相机边界
    // ========================================

    /// <summary>根据地图尺寸设置相机边界限制</summary>
    private void InitCameraBounds()
    {
        if (_camera == null) return;

        float mapWPx = 0, mapHPx = 0;

        if (_fog != null && _fog.MapWidthPx > 0)
        {
            mapWPx = _fog.MapWidthPx;
            mapHPx = _fog.MapHeightPx;
        }
        else if (_grid != null && _grid.MapPixelWidth > 0)
        {
            mapWPx = _grid.MapPixelWidth;
            mapHPx = _grid.MapPixelHeight;
        }

        if (mapWPx <= 0 || mapHPx <= 0)
        {
            GD.PrintErr("[OverworldScene3D] 相机边界跳过: 地图尺寸无效");
            return;
        }

        // PixelToWorld = 1.0 / 156.0 (HexOverworldRenderer3D 中定义)
        const float PixelToWorld = 1.0f / 156.0f;
        _camera.SetMapBounds(mapWPx, mapHPx, PixelToWorld);
    }
}
