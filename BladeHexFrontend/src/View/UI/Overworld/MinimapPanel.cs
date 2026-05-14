// MinimapPanel.cs
// 大地图右上角小地图 — 显示已探索区域的地形颜色、玩家位置、POI 标记、视野矩形框
// 点击小地图可跳转摄像机或寻路到 POI
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// 小地图面板 — 右上角固定 UI，低分辨率 ImageTexture 表示全图。
/// 增量更新：只扫描玩家视野附近的脏区域，不全量遍历。
/// </summary>
[GlobalClass]
public partial class MinimapPanel : PanelContainer
{
    [Signal] public delegate void MinimapClickedEventHandler(Vector2 worldPos);
    [Signal] public delegate void MinimapPoiClickedEventHandler(Vector2 worldPos);

    // ========================================
    // 配置
    // ========================================
    private const int MAP_W = 320;
    private const int MAP_H = 213;
    private const int PANEL_MARGIN = 6;
    private const float UPDATE_INTERVAL = 0.5f;
    private const float PANEL_ALPHA = 0.7f;
    private const int DIRTY_SCAN_RADIUS = 24; // 每次增量扫描的小地图像素半径

    private static readonly Color BgColor = new(0.02f, 0.02f, 0.04f, 0.75f);
    private static readonly Color BorderColor = new(0.4f, 0.35f, 0.25f, 0.7f);
    private static readonly Color FogColor = new(0.0f, 0.0f, 0.0f, 1.0f);
    private static readonly Color PlayerColor = new(1.0f, 0.9f, 0.2f, 1.0f);
    private static readonly Color PoiColor = new(0.9f, 0.6f, 0.2f, 0.9f);
    private static readonly Color ViewRectColor = new(1.0f, 1.0f, 1.0f, 0.6f);

    // ========================================
    // 引用（由 OverworldScene 注入）
    // ========================================
    private FogOfWar? _fog;
    private ChunkManager? _chunkManager;
    private List<OverworldPOI>? _pois;
    private float _mapWidthPx;
    private float _mapHeightPx;

    // ========================================
    // 内部状态
    // ========================================
    private Image _mapImage = null!;
    private ImageTexture _mapTexture = null!;
    private TextureRect _mapRect = null!;
    private Control _overlayControl = null!;
    private Control _contentContainer = null!;
    private Button _toggleBtn = null!;
    private Label _tooltipLabel = null!;
    private bool _collapsed;
    private float _updateTimer;
    private Vector2 _playerWorldPos;
    private Vector2I _lastPlayerMp = new(-1, -1); // 上次绘制时的玩家小地图坐标
    private Rect2 _cameraViewRect;
    private bool _initialized;
    private OverworldPOI? _hoveredPoi;
    private int _revealedPoiCount; // 已绘制的 POI 数量（用于增量检测）
    private float _poiHitRadius; // POI 悬浮/点击的世界坐标容差（动态计算）

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
        // POI 悬浮容差 = 小地图 8 像素对应的世界距离
        _poiHitRadius = (mapWidthPx / MAP_W) * 8.0f;

        BuildUI();
        BakeTerrainBase();
        _initialized = true;
    }

    // ========================================
    // UI 构建
    // ========================================

    private void BuildUI()
    {
        var style = new StyleBoxFlat { BgColor = BgColor };
        style.SetBorderWidthAll(2);
        style.BorderColor = BorderColor;
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(PANEL_MARGIN);
        AddThemeStyleboxOverride("panel", style);

        Modulate = new Color(1, 1, 1, PANEL_ALPHA);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        AddChild(vbox);

        // 顶部：标题 + 收起按钮
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(header);

        var titleLabel = new Label { Text = "地图" };
        titleLabel.AddThemeFontSizeOverride("font_size", 11);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.65f, 0.5f));
        titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(titleLabel);

        _toggleBtn = new Button { Text = "▼", CustomMinimumSize = new Vector2(24, 20) };
        _toggleBtn.AddThemeFontSizeOverride("font_size", 10);
        _toggleBtn.Pressed += ToggleCollapse;
        header.AddChild(_toggleBtn);

        // 内容容器（可收起）
        _contentContainer = new Control { CustomMinimumSize = new Vector2(MAP_W, MAP_H) };
        vbox.AddChild(_contentContainer);

        // 地图纹理
        _mapImage = Image.CreateEmpty(MAP_W, MAP_H, false, Image.Format.Rgba8);
        _mapImage.Fill(FogColor);
        _mapTexture = ImageTexture.CreateFromImage(_mapImage);

        _mapRect = new TextureRect
        {
            Texture = _mapTexture,
            CustomMinimumSize = new Vector2(MAP_W, MAP_H),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        _contentContainer.AddChild(_mapRect);

        // 覆盖层
        _overlayControl = new Control { Name = "Overlay" };
        _overlayControl.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _overlayControl.MouseFilter = MouseFilterEnum.Ignore;
        _mapRect.AddChild(_overlayControl);
        _overlayControl.Draw += OnOverlayDraw;

        // 输入
        _mapRect.GuiInput += OnMapInput;
        _mapRect.MouseExited += () => { _hoveredPoi = null; _tooltipLabel.Visible = false; };

        // POI 悬浮提示
        _tooltipLabel = new Label { Visible = false, ZIndex = 10 };
        _tooltipLabel.AddThemeFontSizeOverride("font_size", 11);
        _tooltipLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.8f));
        _tooltipLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        _tooltipLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _tooltipLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _contentContainer.AddChild(_tooltipLabel);

        CustomMinimumSize = new Vector2(MAP_W + PANEL_MARGIN * 2, 0);
    }

    private void ToggleCollapse()
    {
        _collapsed = !_collapsed;
        _contentContainer.Visible = !_collapsed;
        _toggleBtn.Text = _collapsed ? "▲" : "▼";
    }

    // ========================================
    // 地形底图烘焙（初始化时一次性）
    // ========================================

    private void BakeTerrainBase()
    {
        if (_fog == null || _mapWidthPx <= 0 || _mapHeightPx <= 0) return;

        float scaleX = _mapWidthPx / MAP_W;
        float scaleY = _mapHeightPx / MAP_H;

        for (int py = 0; py < MAP_H; py++)
        {
            float worldY = py * scaleY;
            for (int px = 0; px < MAP_W; px++)
            {
                float worldX = px * scaleX;
                bool explored = IsWorldPixelExplored(worldX, worldY);
                _mapImage.SetPixel(px, py, explored ? SampleTerrainColor(worldX, worldY) : FogColor);
            }
        }

        // 已探索 POI 标记
        _revealedPoiCount = DrawExploredPois();
        _mapTexture.Update(_mapImage);
    }

    /// <summary>绘制所有已探索 POI，返回绘制数量</summary>
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
                if (fx >= 0 && fx < MAP_W && fy >= 0 && fy < MAP_H)
                    _mapImage.SetPixel(fx, fy, PoiColor);
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
            var tile = _chunkManager.GetTile(axial.X, axial.Y);
            if (tile != null)
                return BladeHex.View.Map.HexOverworldRenderer.GetTerrainColor((int)tile.Terrain);
        }
        return new Color(0.05f, 0.08f, 0.15f);
    }

    // ========================================
    // 每帧更新
    // ========================================

    public override void _Process(double delta)
    {
        if (!_initialized || !Visible || _collapsed) return;

        _updateTimer += (float)delta;
        if (_updateTimer >= UPDATE_INTERVAL)
        {
            _updateTimer = 0;
            RefreshDirtyRegion();
        }

        // 覆盖层：只在玩家位置变化时重绘
        var currentMp = new Vector2I((int)WorldToMinimap(_playerWorldPos).X, (int)WorldToMinimap(_playerWorldPos).Y);
        if (currentMp != _lastPlayerMp)
        {
            _lastPlayerMp = currentMp;
            _overlayControl.QueueRedraw();
        }
    }

    public void UpdatePlayerAndCamera(Vector2 playerPos, Vector2 cameraPos, Vector2 cameraZoom, Vector2 viewportSize)
    {
        _playerWorldPos = playerPos;
        Vector2 visibleSize = viewportSize / cameraZoom;
        _cameraViewRect = new Rect2(cameraPos - visibleSize * 0.5f, visibleSize);
        // 视野框变化也需要重绘覆盖层
        _overlayControl.QueueRedraw();
    }

    // ========================================
    // 增量刷新（只扫描玩家附近的脏区域）
    // ========================================

    private void RefreshDirtyRegion()
    {
        if (_fog == null) return;

        // 计算玩家在小地图上的位置，以此为中心扫描
        var playerMp = WorldToMinimap(_playerWorldPos);
        int cx = (int)playerMp.X, cy = (int)playerMp.Y;

        int xMin = Mathf.Max(0, cx - DIRTY_SCAN_RADIUS);
        int xMax = Mathf.Min(MAP_W - 1, cx + DIRTY_SCAN_RADIUS);
        int yMin = Mathf.Max(0, cy - DIRTY_SCAN_RADIUS);
        int yMax = Mathf.Min(MAP_H - 1, cy + DIRTY_SCAN_RADIUS);

        float scaleX = _mapWidthPx / MAP_W;
        float scaleY = _mapHeightPx / MAP_H;
        bool dirty = false;

        for (int py = yMin; py <= yMax; py++)
        {
            float worldY = py * scaleY;
            for (int px = xMin; px <= xMax; px++)
            {
                var current = _mapImage.GetPixel(px, py);
                // 只处理仍为黑色（未探索）的像素
                if (current.R > 0.02f || current.G > 0.02f || current.B > 0.05f) continue;

                float worldX = px * scaleX;
                if (IsWorldPixelExplored(worldX, worldY))
                {
                    _mapImage.SetPixel(px, py, SampleTerrainColor(worldX, worldY));
                    dirty = true;
                }
            }
        }

        // 检查是否有新 POI 被揭示
        if (_pois != null)
        {
            int currentExplored = CountExploredPois();
            if (currentExplored > _revealedPoiCount)
            {
                _revealedPoiCount = DrawExploredPois();
                dirty = true;
            }
        }

        if (dirty)
            _mapTexture.Update(_mapImage);
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

    private void OnOverlayDraw()
    {
        // 视野矩形框
        if (_cameraViewRect.Size.X > 0 && _mapWidthPx > 0)
        {
            var topLeft = WorldToMinimap(_cameraViewRect.Position);
            var size = new Vector2(
                _cameraViewRect.Size.X / _mapWidthPx * MAP_W,
                _cameraViewRect.Size.Y / _mapHeightPx * MAP_H);
            _overlayControl.DrawRect(new Rect2(topLeft, size), ViewRectColor, false, 1.5f);
        }

        // 玩家位置（闪烁）
        var playerMp = WorldToMinimap(_playerWorldPos);
        float pulse = 0.7f + 0.3f * Mathf.Sin((float)Time.GetTicksMsec() / 300.0f);
        _overlayControl.DrawCircle(playerMp, 3.5f, new Color(PlayerColor.R, PlayerColor.G, PlayerColor.B, pulse));
    }

    // ========================================
    // 输入处理
    // ========================================

    private void OnMapInput(InputEvent ev)
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
            if (dist < bestDist)
            {
                bestDist = dist;
                best = poi;
            }
        }
        return best;
    }

    // ========================================
    // 坐标转换
    // ========================================

    private Vector2 WorldToMinimap(Vector2 worldPos)
    {
        return new Vector2(
            Mathf.Clamp(worldPos.X / _mapWidthPx * MAP_W, 0, MAP_W - 1),
            Mathf.Clamp(worldPos.Y / _mapHeightPx * MAP_H, 0, MAP_H - 1));
    }

    private Vector2 MinimapToWorld(Vector2 minimapPos)
    {
        return new Vector2(
            minimapPos.X / MAP_W * _mapWidthPx,
            minimapPos.Y / MAP_H * _mapHeightPx);
    }
}
