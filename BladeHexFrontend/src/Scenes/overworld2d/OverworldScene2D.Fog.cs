// OverworldScene2D.Fog.cs
// 迷雾系统 — 从 OverworldScene3D.Fog.cs 迁移
// 使用 CanvasItem shader 替代 3D mesh
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.AssetSystem;
using BladeHex.View.Map;
using BladeHex.Strategic;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    // ========================================
    // 迷雾系统
    // ========================================

    private FogOfWar? _fog;
    private FogOverlay2D? _fogOverlay;
    private TerritoryOverlay? _territoryOverlay;
    private Dictionary<string, NationTerritory>? _worldTerritories;
    private List<NationConfig>? _worldNations;

    /// <summary>已流式加载装饰层的 tile 坐标集合（不代表 ground/shader cache）。</summary>
    private readonly HashSet<Vector2I> _streamedDecorationTileCoords = new();
    private readonly List<HexOverworldTile> _streamingDecorationLoadBuffer = new();

    // ========================================
    // 初始化
    // ========================================

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

        GD.Print($"[OverworldScene2D] 迷雾: {_fog.GridW}×{_fog.GridH} grid, cell={cellSz}px");

        // 初始化迷雾覆盖层（CanvasItem shader）
        InitFogOverlay(mapWPx, mapHPx);

        // 订阅聚落易手事件
        PoiTransferService.PoiTransferred += OnPoiTransferredForFog;
    }

    /// <summary>初始化迷雾覆盖层</summary>
    private void InitFogOverlay(int mapWPx, int mapHPx)
    {
        if (_fog == null) return;

        _fogOverlay = new FogOverlay2D();
        _fogOverlay.Name = "FogOverlay2D";
        _fogOverlay.ZIndex = 100; // 确保在地图上方
        AddChild(_fogOverlay);
        _fogOverlay.Initialize(_fog, mapWPx, mapHPx);

        GD.Print("[OverworldScene2D] 迷雾覆盖层初始化完成");
    }

    /// <summary>初始化国家领土覆盖层</summary>
    private void InitTerritoryOverlay()
    {
        if (_worldTerritories == null || _worldNations == null || _fog == null) return;

        _territoryOverlay = new TerritoryOverlay();
        _territoryOverlay.Name = "TerritoryOverlay";
        AddChild(_territoryOverlay);
        // 2D 版本：TerritoryOverlay.Initialize 的 sceneRoot 参数是 Node3D，传 null
        _territoryOverlay.Initialize(_worldTerritories, _worldNations, _fog.MapWidthPx, _fog.MapHeightPx, null);

        GD.Print($"[OverworldScene2D] 领土覆盖层初始化完成");
    }

    /// <summary>揭示玩家出身种族的母国领土</summary>
    private void RevealHomeTerritory()
    {
        if (_fog == null) return;

        var playerRace = (BladeHex.Data.RaceData.Race)PlayerRaceId;

        // 有实际领土数据 → 精确揭示
        if (_worldTerritories != null && _worldNations != null)
        {
            var homeNation = RaceNationMapping.FindHomeNation(playerRace, _worldTerritories, _worldNations);
            if (homeNation != null && _worldTerritories.TryGetValue(homeNation.Id, out var territory))
            {
                _fog.RevealTerritory(territory.AllTiles);
                GD.Print($"[OverworldScene2D] 母国领土揭示: {homeNation.DisplayName}, {territory.TotalTiles} tiles");
                return;
            }
        }

        // 无领土数据 → fallback
        _fog.RevealRaceRegionFallback(playerRace);
        GD.Print($"[OverworldScene2D] 母国领土揭示: fallback (race={playerRace})");
    }

    // ========================================
    // 每帧更新
    // ========================================

    /// <summary>每帧更新迷雾视野 + 增量加载已揭示 tile 的装饰层</summary>
    private void UpdateFog()
    {
        if (_fog == null) return;

        // 设置视野范围为 15 格 hex 半径（像素），天气会缩减视野
        float baseVision = 15.0f * HexOverworldTile.HexSize * 1.732f; // ≈ 4050px
        _fog.VisionRange = baseVision * WeatherVisionFactor;

        // 更新视野（玩家周围变为 InVision，远处变为 Revealed）
        _fog.UpdateVision(_playerPixelPos);

        // 标记迷雾覆盖层需要刷新
        _fogOverlay?.MarkDirty(_playerPixelPos);

        // 增量揭示新 tile 的装饰层；ground/shader cache 已在开局全图加载。
        LoadNewlyRevealedStreamingDecorations();
    }

    /// <summary>将新揭示的 tile 增量加载到装饰层渲染器；不触碰 ground/shader cache。</summary>
    private void LoadNewlyRevealedStreamingDecorations()
    {
        if (_fog == null) return;

        var playerAxial = HexOverworldTile.PixelToAxial(_playerPixelPos.X, _playerPixelPos.Y);
        int checkRadius = 18; // 略大于视野半径，确保边缘 tile 也被渲染

        BeginStreamingDecorationBatch();

        for (int dq = -checkRadius; dq <= checkRadius; dq++)
        {
            int r1 = Mathf.Max(-checkRadius, -dq - checkRadius);
            int r2 = Mathf.Min(checkRadius, -dq + checkRadius);
            for (int dr = r1; dr <= r2; dr++)
            {
                int q = playerAxial.X + dq;
                int r = playerAxial.Y + dr;
                var coord = new Vector2I(q, r);

                if (_streamedDecorationTileCoords.Contains(coord)) continue;

                HexOverworldTile? tile = _mapAccess.GetActiveTile(q, r);

                if (tile == null) continue;
                if (!_fog.IsRevealed(tile.PixelPos.X, tile.PixelPos.Y)) continue;

                AddStreamingDecorationTile(tile);
            }
        }

        FlushStreamingDecorationBatch();
    }

    /// <summary>加载当前可加载范围内已揭示 tile 的装饰层；chunk 模式不会扫描未激活缓存。</summary>
    private void LoadRevealedStreamingDecorationsInActiveRange()
    {
        if (_fog == null) return;

        var startTime = Time.GetTicksMsec();
        BeginStreamingDecorationBatch();

        if (_chunkManager != null)
        {
            // Chunk 模式：遍历所有活跃 chunk 中的 tile
            foreach (var kvp in _mapAccess.ActiveChunks)
            {
                foreach (var tile in kvp.Value.Tiles.Values)
                {
                    if (_streamedDecorationTileCoords.Contains(tile.Coord)) continue;
                    if (!_fog.IsRevealed(tile.PixelPos.X, tile.PixelPos.Y)) continue;

                    AddStreamingDecorationTile(tile);
                }
            }

        }
        else if (_grid != null)
        {
            // 非 chunk 模式：遍历 grid 中所有 tile
            foreach (var tile in _grid.Tiles.Values)
            {
                if (_streamedDecorationTileCoords.Contains(tile.Coord)) continue;
                if (!_fog.IsRevealed(tile.PixelPos.X, tile.PixelPos.Y)) continue;

                AddStreamingDecorationTile(tile);
            }
        }

        int loadedCount = _streamingDecorationLoadBuffer.Count;
        FlushStreamingDecorationBatch();

        var elapsed = Time.GetTicksMsec() - startTime;
        GD.Print($"[OverworldScene2D] 已加载区域揭示装饰层: {loadedCount} tiles, {elapsed}ms");
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
            GD.PrintErr("[OverworldScene2D] 相机边界跳过: 地图尺寸无效");
            return;
        }

        _camera.SetMapBounds(mapWPx, mapHPx);

        GD.Print($"[OverworldScene2D] 相机边界: {mapWPx}×{mapHPx}px");
    }

    /// <summary>检查位置是否已揭示</summary>
    private bool IsFogRevealed(float px, float py)
    {
        return _fog == null || _fog.IsRevealed(px, py);
    }

    private void OnPoiTransferredForFog(PoiTransferEvent evt)
    {
        if (evt?.Poi == null || _fog == null) return;

        string? playerFaction = null;
        if (_reputationTracker != null && EconomyMgr != null)
        {
            playerFaction = new PlayerNationResolver().GetCurrent(_reputationTracker, EconomyMgr.DaysPassed);
        }

        if (string.IsNullOrEmpty(playerFaction)) return;

        // 失去聚落 → 覆盖迷雾
        if (evt.OldFaction == playerFaction && evt.NewFaction != playerFaction)
        {
            _fog.HideArea(evt.Poi.Position, 600.0f);
            _fogOverlay?.MarkDirty(evt.Poi.Position);
            GD.Print($"[FogOfWar] 失去了聚落 {evt.Poi.PoiName}，其周围迷雾已重置！");
        }
        // 夺回聚落 → 揭示迷雾
        else if (evt.NewFaction == playerFaction)
        {
            _fog.RevealArea(evt.Poi.Position, 800.0f);
            _fogOverlay?.MarkDirty(evt.Poi.Position);
            GD.Print($"[FogOfWar] 夺回了聚落 {evt.Poi.PoiName}，已自动探明其周围迷雾！");
        }
    }
}

// ========================================
// FogOverlay2D — CanvasItem shader 迷雾覆盖层
// ========================================

/// <summary>
/// 2D 版迷雾覆盖层 — 使用 CanvasItem shader。
///
/// 工作原理：
/// 1. 一个全地图大小的 Sprite2D 覆盖在地图上方
/// 2. Shader 读取迷雾状态纹理（R通道=不透明度）
/// 3. 未探索区域显示深色迷雾，已揭示区域透明
/// 4. 视野边缘有柔和渐变过渡
///
/// 纹理槽位：
/// - fog_mask: 动态生成的迷雾状态纹理（每帧更新脏区域）
/// </summary>
public partial class FogOverlay2D : Node2D
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>未探索区域不透明度</summary>
    public float UnexploredOpacity { get; set; } = 0.92f;

    /// <summary>已揭示但不在视野内的区域不透明度</summary>
    public float RevealedOpacity { get; set; } = 0.0f;

    /// <summary>迷雾颜色（深蓝/黑色）</summary>
    public Color FogColor { get; set; } = new Color(0.05f, 0.05f, 0.12f, 1.0f);

    /// <summary>已揭示区域色调</summary>
    public Color RevealedTint { get; set; } = new Color(0.15f, 0.15f, 0.25f, 1.0f);

    // ========================================
    // 内部状态
    // ========================================

    private FogOfWar? _fogData;
    private ImageTexture? _fogMaskTexture;
    private Image? _fogMaskImage;
    private ShaderMaterial? _material;
    private Sprite2D? _sprite;

    private int _maskW;
    private int _maskH;
    private bool _dirty = true;
    private int _updateFrameSkip = 0;

    // 世界尺寸（像素）
    private float _worldWidthPx;
    private float _worldHeightPx;

    // 增量更新：上次玩家所在的 fog cell
    private Vector2I _lastPlayerCell = new(-1, -1);
    private Vector2 _dirtyCenterWorld = Vector2.Zero;

    // ========================================
    // 初始化
    // ========================================

    /// <summary>
    /// 初始化迷雾覆盖层
    /// </summary>
    /// <param name="fogData">迷雾数据源</param>
    /// <param name="worldWidthPx">世界宽度（像素）</param>
    /// <param name="worldHeightPx">世界高度（像素）</param>
    public void Initialize(FogOfWar fogData, float worldWidthPx, float worldHeightPx)
    {
        _fogData = fogData;
        _worldWidthPx = worldWidthPx;
        _worldHeightPx = worldHeightPx;
        _maskW = fogData.GridW;
        _maskH = fogData.GridH;

        // 创建迷雾 mask 纹理（R=不透明度，单通道足够）
        _fogMaskImage = Image.CreateEmpty(_maskW, _maskH, false, Image.Format.Rgba8);
        _fogMaskImage.Fill(new Color(1, 0, 0, 1)); // 初始全不透明（未探索）
        _fogMaskTexture = ImageTexture.CreateFromImage(_fogMaskImage);

        // 创建 sprite 显示迷雾
        _sprite = new Sprite2D();
        _sprite.Name = "FogSprite";
        _sprite.Texture = _fogMaskTexture;
        _sprite.Centered = false;
        _sprite.Scale = new Vector2(worldWidthPx / _maskW, worldHeightPx / _maskH);
        AddChild(_sprite);

        // 创建 shader material
        CreateShaderMaterial();

        // 初始全量更新（确保已揭示领土正确显示）
        FullUpdateFogMask();

        GD.Print($"[FogOverlay2D] 初始化: {_maskW}×{_maskH} mask, world={worldWidthPx:F0}×{worldHeightPx:F0}px");
    }

    /// <summary>加载 shader 并创建 material</summary>
    private void CreateShaderMaterial()
    {
        _material = new ShaderMaterial();

        // 尝试从文件加载 shader（先检查文件是否存在）
        Shader? shader = null;
        string shaderPath = "res://BladeHexFrontend/src/assets/shaders/fog_overlay_2d.gdshader";
        if (ResourceLoader.Exists(shaderPath))
        {
            shader = ShaderAssetResolver.Load("fog_overlay_2d", shaderPath);
        }

        // 如果文件不存在或加载失败，创建内联 shader
        if (shader == null)
        {
            shader = new Shader();
            shader.Code = @"
shader_type canvas_item;

uniform sampler2D fog_mask : filter_nearest, repeat_disable;
uniform float unexplored_opacity : hint_range(0.0, 1.0) = 0.92;
uniform float revealed_opacity : hint_range(0.0, 1.0) = 0.0;
uniform float edge_softness : hint_range(0.0, 0.5) = 0.08;
uniform vec4 fog_color : source_color = vec4(0.05, 0.05, 0.12, 1.0);
uniform vec4 revealed_tint : source_color = vec4(0.15, 0.15, 0.25, 1.0);

void fragment() {
    float fog_value = texture(fog_mask, UV).r;
    float alpha = smoothstep(0.0, edge_softness, fog_value);
    alpha *= unexplored_opacity;
    vec3 color = mix(revealed_tint.rgb, fog_color.rgb, fog_value);
    COLOR = vec4(color, alpha);
}
";
            GD.Print("[FogOverlay2D] 使用内联 shader");
        }
        else
        {
            GD.Print($"[FogOverlay2D] 从文件加载 shader: {shaderPath}");
        }

        _material.Shader = shader;

        // 设置 uniform
        _material.SetShaderParameter("fog_mask", _fogMaskTexture!);
        _material.SetShaderParameter("unexplored_opacity", UnexploredOpacity);
        _material.SetShaderParameter("revealed_opacity", RevealedOpacity);
        _material.SetShaderParameter("fog_color", FogColor);
        _material.SetShaderParameter("revealed_tint", RevealedTint);

        // 将 material 应用到 sprite
        _sprite!.Material = _material;
    }

    // ========================================
    // 每帧更新
    // ========================================

    public override void _Process(double delta)
    {
        // 每 3 帧更新一次 mask（性能优化）
        _updateFrameSkip++;
        if (_updateFrameSkip < 3) return;
        _updateFrameSkip = 0;

        if (_dirty && _fogData != null)
        {
            UpdateFogMask();
            _dirty = false;
        }
    }

    /// <summary>标记迷雾数据已变化，需要刷新 mask</summary>
    public void MarkDirty(Vector2 worldPosition)
    {
        _dirtyCenterWorld = worldPosition;
        _dirty = true;
    }

    public void MarkDirty()
    {
        _dirty = true;
    }

    // ========================================
    // Mask 更新（增量：只更新玩家视野附近的脏区域）
    // ========================================

    /// <summary>全量更新 mask（初始化或 reveal_all 时调用）</summary>
    public void FullUpdateFogMask()
    {
        if (_fogData == null || _fogMaskImage == null || _fogMaskTexture == null) return;

        for (int gy = 0; gy < _maskH; gy++)
        {
            for (int gx = 0; gx < _maskW; gx++)
            {
                byte state = _fogData.ExploredGrid[gy, gx];
                float opacity = state == (byte)FogOfWar.FogState.Unexplored ? 1.0f : 0.0f;
                _fogMaskImage.SetPixel(gx, gy, new Color(opacity, 0, 0, 1));
            }
        }

        _fogMaskTexture.Update(_fogMaskImage);
    }

    /// <summary>增量更新 mask（只更新玩家视野附近的脏区域）</summary>
    private void UpdateFogMask()
    {
        if (_fogData == null || _fogMaskImage == null || _fogMaskTexture == null) return;

        // 计算需要更新的区域（玩家视野半径 + 边距）
        int rangeCells = (int)(_fogData.VisionRange / _fogData.CellSize) + 4;

        // 获取当前玩家 cell
        Vector2I currentCell = WorldToFogCell(_dirtyCenterWorld);

        int minGx = Mathf.Max(0, currentCell.X - rangeCells);
        int maxGx = Mathf.Min(_maskW - 1, currentCell.X + rangeCells);
        int minGy = Mathf.Max(0, currentCell.Y - rangeCells);
        int maxGy = Mathf.Min(_maskH - 1, currentCell.Y + rangeCells);

        // 也包含上次位置的区域（确保离开的区域也被更新）
        if (_lastPlayerCell.X >= 0)
        {
            minGx = Mathf.Min(minGx, Mathf.Max(0, _lastPlayerCell.X - rangeCells));
            maxGx = Mathf.Max(maxGx, Mathf.Min(_maskW - 1, _lastPlayerCell.X + rangeCells));
            minGy = Mathf.Min(minGy, Mathf.Max(0, _lastPlayerCell.Y - rangeCells));
            maxGy = Mathf.Max(maxGy, Mathf.Min(_maskH - 1, _lastPlayerCell.Y + rangeCells));
        }

        _lastPlayerCell = currentCell;

        // 增量更新脏区域
        for (int gy = minGy; gy <= maxGy; gy++)
        {
            for (int gx = minGx; gx <= maxGx; gx++)
            {
                byte state = _fogData.ExploredGrid[gy, gx];
                float opacity = state == (byte)FogOfWar.FogState.Unexplored ? 1.0f : 0.0f;
                _fogMaskImage.SetPixel(gx, gy, new Color(opacity, 0, 0, 1));
            }
        }

        _fogMaskTexture.Update(_fogMaskImage);
    }

    private Vector2I WorldToFogCell(Vector2 worldPosition)
    {
        if (_fogData == null) return Vector2I.Zero;

        int gx = Mathf.Clamp((int)(worldPosition.X / _fogData.CellSize), 0, _maskW - 1);
        int gy = Mathf.Clamp((int)(worldPosition.Y / _fogData.CellSize), 0, _maskH - 1);
        return new Vector2I(gx, gy);
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>设置未探索区域不透明度</summary>
    public void SetUnexploredOpacity(float opacity)
    {
        UnexploredOpacity = opacity;
        _material?.SetShaderParameter("unexplored_opacity", opacity);
    }

    /// <summary>获取迷雾 mask 纹理（供其他组件共享）</summary>
    public ImageTexture? GetFogMaskTexture() => _fogMaskTexture;
}
