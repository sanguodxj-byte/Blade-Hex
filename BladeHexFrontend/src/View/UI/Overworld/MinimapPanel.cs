// MinimapPanel.cs
// 大地图右上角小地图 — 显示已探索区域的地形颜色、玩家位置、POI 标记、视野矩形框
// 点击小地图可跳转摄像机或寻路到 POI
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.UI.Minimap;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 大地图小地图面板 — 右上角固定 UI，低分辨率 ImageTexture 表示全图。
/// 增量更新：只扫描玩家视野附近的脏区域，不全量遍历。
/// </summary>
[GlobalClass]
public partial class MinimapPanel : MinimapPanelBase
{
    [Signal] public delegate void MinimapPoiClickedEventHandler(Vector2 worldPos);

    // ========================================
    // 配置覆盖
    // ========================================
    protected override int MapPixelWidth => 320;
    protected override int MapPixelHeight => 213;
    protected override float PanelAlpha => 0.7f;
    protected override string Title => "地图";

    // ========================================
    // 常量
    // ========================================
    private const float UPDATE_INTERVAL = 0.5f;
    private const int DIRTY_SCAN_RADIUS = 24;

    private static readonly Color FogColor = new(0.0f, 0.0f, 0.0f, 1.0f);
    private static readonly Color PlayerColor = new(1.0f, 0.9f, 0.2f, 1.0f);
    private static readonly Color PoiColor = new(0.9f, 0.6f, 0.2f, 0.9f);

    // ========================================
    // 引用（由 OverworldScene3D 注入）
    // ========================================
    private FogOfWar? _fog;
    private ChunkManager? _chunkManager;
    private List<OverworldPOI>? _pois;
    private float _mapWidthPx;
    private float _mapHeightPx;

    // ========================================
    // 内部状态
    // ========================================
    private Label _tooltipLabel = null!;
    private float _updateTimer;
    private Vector2 _playerWorldPos;
    private Vector2I _lastPlayerMp = new(-1, -1);
    private Rect2 _cameraViewRect;
    private OverworldPOI? _hoveredPoi;
    private int _revealedPoiCount;
    private float _poiHitRadius;

    // ========================================
    // 初始化
    // ========================================

    public void Initialize(FogOfWar fog, ChunkManager? chunkManager, List<OverworldPOI> pois,
        float mapWidthPx, float mapHeightPx)
    {
        _fog = fog;
        _chunkManager = chunkManager;
        _pois = pois;
        _mapWidthPx = mapWidthPx;
        _mapHeightPx = mapHeightPx;
        _poiHitRadius = (mapWidthPx / MapPixelWidth) * 8.0f;

        BuildBaseUI();
        BuildTooltip();
        BakeTerrainBase();
        Initialized = true;
    }

    private void BuildTooltip()
    {
        _tooltipLabel = new Label { Visible = false, ZIndex = 10 };
        _tooltipLabel.AddThemeFontSizeOverride("font_size", 11);
        _tooltipLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.8f));
        _tooltipLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        _tooltipLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _tooltipLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        ContentContainer.AddChild(_tooltipLabel);

        MapRect.MouseExited += () => { _hoveredPoi = null; _tooltipLabel.Visible = false; };
    }

    // ========================================
    // 地形底图烘焙
    // ========================================

    private void BakeTerrainBase()
    {
        if (_fog == null || _mapWidthPx <= 0 || _mapHeightPx <= 0) return;

        float scaleX = _mapWidthPx / MapPixelWidth;
        float scaleY = _mapHeightPx / MapPixelHeight;

        for (int py = 0; py < MapPixelHeight; py++)
        {
            float worldY = py * scaleY;
            for (int px = 0; px < MapPixelWidth; px++)
            {
                float worldX = px * scaleX;
                bool explored = IsWorldPixelExplored(worldX, worldY);
                MapImage.SetPixel(px, py, explored ? SampleTerrainColor(worldX, worldY) : FogColor);
            }
        }

        _revealedPoiCount = DrawExploredPois();
        FlushTexture();
    }

    private int DrawExploredPois()
    {
        if (_pois == null || _fog == null) return 0;
        int count = 0;
        foreach (var poi in _pois)
        {
            if (!IsWorldPixelExplored(poi.Position.X, poi.Position.Y)) continue;
            DrawPoiDot(poi.Position);
            count++;
        }
        return count;
    }

    private void DrawPoiDot(Vector2 worldPos)
    {
        var mp = WorldToMinimap(worldPos);
        int mx = (int)mp.X, my = (int)mp.Y;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int fx = mx + dx, fy = my + dy;
                if (fx >= 0 && fx < MapPixelWidth && fy >= 0 && fy < MapPixelHeight)
                    MapImage.SetPixel(fx, fy, PoiColor);
            }
    }

    private bool IsWorldPixelExplored(float worldX, float worldY)
    {
        if (_fog == null) return false;
        int gx = (int)(worldX / _fog.CellSize);
        int gy = (int)(worldY / _fog.CellSize);
        if (gx < 0 || gx >= _fog.GridW || gy < 0 || gy >= _fog.GridH) return false;
        return _fog.ExploredGrid[gy, gx] != (byte)FogOfWar.FogState.Unexplored;
    }

    private Color SampleTerrainColor(float worldX, float worldY)
    {
        if (_chunkManager != null)
        {
            var axial = HexOverworldTile.PixelToAxial(worldX, worldY);
            var tile = _chunkManager.GetTileAnywhere(axial.X, axial.Y);
            if (tile != null)
                return TerrainVisualRegistry.Get(tile.Terrain).DominantColor;
        }
        return new Color(0.05f, 0.08f, 0.15f);
    }

    // ========================================
    // 每帧更新
    // ========================================

    public override void _Process(double delta)
    {
        if (!Initialized || !Visible || IsCollapsed) return;

        _updateTimer += (float)delta;
        if (_updateTimer >= UPDATE_INTERVAL)
        {
            _updateTimer = 0;
            RefreshDirtyRegion();
        }

        var currentMp = new Vector2I((int)WorldToMinimap(_playerWorldPos).X, (int)WorldToMinimap(_playerWorldPos).Y);
        if (currentMp != _lastPlayerMp)
        {
            _lastPlayerMp = currentMp;
            RequestOverlayRedraw();
        }
    }

    public void UpdatePlayerAndCamera(Vector2 playerPos, Vector2 cameraPos, Vector2 cameraZoom, Vector2 viewportSize)
    {
        _playerWorldPos = playerPos;
        Vector2 visibleSize = viewportSize / cameraZoom;
        _cameraViewRect = new Rect2(cameraPos - visibleSize * 0.5f, visibleSize);
        RequestOverlayRedraw();
    }

    // ========================================
    // 增量刷新
    // ========================================

    private void RefreshDirtyRegion()
    {
        if (_fog == null) return;

        var playerMp = WorldToMinimap(_playerWorldPos);
        int cx = (int)playerMp.X, cy = (int)playerMp.Y;

        int xMin = Mathf.Max(0, cx - DIRTY_SCAN_RADIUS);
        int xMax = Mathf.Min(MapPixelWidth - 1, cx + DIRTY_SCAN_RADIUS);
        int yMin = Mathf.Max(0, cy - DIRTY_SCAN_RADIUS);
        int yMax = Mathf.Min(MapPixelHeight - 1, cy + DIRTY_SCAN_RADIUS);

        float scaleX = _mapWidthPx / MapPixelWidth;
        float scaleY = _mapHeightPx / MapPixelHeight;
        bool dirty = false;

        for (int py = yMin; py <= yMax; py++)
        {
            float worldY = py * scaleY;
            for (int px = xMin; px <= xMax; px++)
            {
                var current = MapImage.GetPixel(px, py);
                if (current.R > 0.02f || current.G > 0.02f || current.B > 0.05f) continue;

                float worldX = px * scaleX;
                if (IsWorldPixelExplored(worldX, worldY))
                {
                    MapImage.SetPixel(px, py, SampleTerrainColor(worldX, worldY));
                    dirty = true;
                }
            }
        }

        if (_pois != null)
        {
            int currentExplored = CountExploredPois();
            if (currentExplored > _revealedPoiCount)
            {
                _revealedPoiCount = DrawExploredPois();
                dirty = true;
            }
        }

        if (dirty) FlushTexture();
    }

    private int CountExploredPois()
    {
        if (_pois == null) return 0;
        int count = 0;
        foreach (var poi in _pois)
            if (IsWorldPixelExplored(poi.Position.X, poi.Position.Y)) count++;
        return count;
    }

    // ========================================
    // 覆盖层绘制
    // ========================================

    protected override void DrawOverlay(Control overlay)
    {
        // 视野矩形框
        if (_cameraViewRect.Size.X > 0 && _mapWidthPx > 0)
        {
            var topLeft = WorldToMinimap(_cameraViewRect.Position);
            var size = new Vector2(
                _cameraViewRect.Size.X / _mapWidthPx * MapPixelWidth,
                _cameraViewRect.Size.Y / _mapHeightPx * MapPixelHeight);
            DrawViewRect(overlay, new Rect2(topLeft, size));
        }

        // 玩家位置（闪烁）
        var playerMp = WorldToMinimap(_playerWorldPos);
        DrawPulsingDot(overlay, playerMp, PlayerColor);
    }

    // ========================================
    // 输入处理
    // ========================================

    protected override void HandleMapInput(InputEvent ev)
    {
        if (ev is InputEventMouseMotion motion)
        {
            var worldPos = MinimapToWorld(motion.Position);
            _hoveredPoi = FindNearestExploredPoi(worldPos, _poiHitRadius);
            if (_hoveredPoi != null)
            {
                _tooltipLabel.Text = _hoveredPoi.PoiName;
                _tooltipLabel.Visible = true;
                _tooltipLabel.Position = motion.Position + new Vector2(8, -20);
            }
            else
            {
                _tooltipLabel.Visible = false;
            }
        }
        else if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            var worldPos = MinimapToWorld(mb.Position);
            var clickedPoi = FindNearestExploredPoi(worldPos, _poiHitRadius);
            if (clickedPoi != null)
                EmitSignal(SignalName.MinimapPoiClicked, clickedPoi.Position);
            else
                EmitSignal(SignalName.MinimapClicked, worldPos);
        }
    }

    private OverworldPOI? FindNearestExploredPoi(Vector2 worldPos, float maxDist)
    {
        if (_pois == null || _fog == null) return null;
        OverworldPOI? best = null;
        float bestDist = maxDist;
        foreach (var poi in _pois)
        {
            if (!IsWorldPixelExplored(poi.Position.X, poi.Position.Y)) continue;
            float dist = worldPos.DistanceTo(poi.Position);
            if (dist < bestDist) { bestDist = dist; best = poi; }
        }
        return best;
    }

    // ========================================
    // 坐标转换
    // ========================================

    protected override Vector2 WorldToMinimap(Vector2 worldPos)
    {
        return new Vector2(
            Mathf.Clamp(worldPos.X / _mapWidthPx * MapPixelWidth, 0, MapPixelWidth - 1),
            Mathf.Clamp(worldPos.Y / _mapHeightPx * MapPixelHeight, 0, MapPixelHeight - 1));
    }

    protected override Vector2 MinimapToWorld(Vector2 minimapPos)
    {
        return new Vector2(
            minimapPos.X / MapPixelWidth * _mapWidthPx,
            minimapPos.Y / MapPixelHeight * _mapHeightPx);
    }
}
