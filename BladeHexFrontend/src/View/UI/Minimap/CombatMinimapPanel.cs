// CombatMinimapPanel.cs
// 战斗小地图 — 嵌入右下角角色面板内，显示战场地形纹理、单位位置、视野框
using Godot;
using BladeHex.Map;
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.UI.Minimap;

/// <summary>
/// 战斗小地图面板 — 显示战场全貌、地形纹理颜色、单位位置、相机视野范围。
/// 嵌入 CombatUI 底部面板右侧。
/// </summary>
[GlobalClass]
public partial class CombatMinimapPanel : MinimapPanelBase
{
    // ========================================
    // 配置覆盖
    // ========================================
    // 渲染分辨率:固定 150×150 像素(不管战场多大,都缩放到这个分辨率)
    // 这样 TextureRect 有足够像素可见,不会出现"15×15 像素几乎不可见"的问题
    private const int RenderResolution = 150;
    protected override int MapPixelWidth => RenderResolution;
    protected override int MapPixelHeight => RenderResolution;
    protected override int PanelMargin => 4;
    protected override float PanelAlpha => 0.85f;
    protected override string Title => "";
    protected override bool ShowHeader => false;
    protected override bool Collapsible => false;

    // ========================================
    // 颜色
    // ========================================
    private static readonly Color PlayerUnitColor = new(0.3f, 0.7f, 1.0f, 1.0f);
    private static readonly Color EnemyUnitColor = new(1.0f, 0.3f, 0.3f, 1.0f);
    private static readonly Color ViewportColor = new(1.0f, 1.0f, 1.0f, 0.8f);

    // ========================================
    // 引用
    // ========================================
    private HexGrid? _hexGrid;
    private CombatManager? _combatMgr;
    private int _mapW = 12;
    private int _mapH = 10;
    // axial → pixel 偏移(让负坐标映射到 [0, mapW/mapH) 范围)
    private int _offsetQ;
    private int _offsetR;

    // 视野数据（由 CombatSceneBase 更新）
    private Vector2 _viewCenter;
    private Vector2 _viewExtent = new(5, 4);

    // 世界坐标尺寸（用于坐标转换）
    private float _worldWidth;
    private float _worldHeight;

    // ========================================
    // 初始化
    // ========================================

    /// <summary>初始化战斗小地图</summary>
    public void Initialize(HexGrid hexGrid, CombatManager combatMgr, int mapWidth, int mapHeight)
    {
        _hexGrid = hexGrid;
        _combatMgr = combatMgr;

        // 六边形地图:axial 坐标范围 [-N, N],像素图大小 = (2N+1) × (2N+1)
        // 矩形地图:直接用 W × H
        if (_hexGrid.Cells.Count > 0)
        {
            // 从实际 cell 坐标推算范围
            int minQ = int.MaxValue, maxQ = int.MinValue;
            int minR = int.MaxValue, maxR = int.MinValue;
            foreach (var coord in _hexGrid.Cells.Keys)
            {
                if (coord.X < minQ) minQ = coord.X;
                if (coord.X > maxQ) maxQ = coord.X;
                if (coord.Y < minR) minR = coord.Y;
                if (coord.Y > maxR) maxR = coord.Y;
            }
            _offsetQ = -minQ;
            _offsetR = -minR;
            _mapW = maxQ - minQ + 1;
            _mapH = maxR - minR + 1;
        }
        else
        {
            _offsetQ = 0;
            _offsetR = 0;
            _mapW = mapWidth;
            _mapH = mapHeight;
        }

        // 世界尺寸 = 格数 × 格间距
        _worldWidth = _mapW * HexUtils.HorizontalSpacing;
        _worldHeight = _mapH * HexUtils.VerticalSpacing;

        BuildBaseUI();
        BakeTerrain();
        Initialized = true;

        // 正方形 150×150 渲染图,面板也正方形
        CustomMinimumSize = new Vector2(RenderResolution, RenderResolution);
    }

    // ========================================
    // 地形烘焙（一次性，战斗地形不变）
    // ========================================

    private void BakeTerrain()
    {
        if (_hexGrid == null) return;

        MapImage.Fill(DefaultBgColor);

        // axial → pixel 转换(pointy-top hex):
        //   px = size * (sqrt(3) * q + sqrt(3)/2 * r)
        //   py = size * (3/2 * r)
        // 我们用归一化版本:把所有 cell 的像素坐标映射到 [0, RenderResolution) 范围

        // 第一遍:算所有 cell 的像素坐标范围
        float sqrt3 = Mathf.Sqrt(3f);
        float minPx = float.MaxValue, maxPx = float.MinValue;
        float minPy = float.MaxValue, maxPy = float.MinValue;

        foreach (var coord in _hexGrid.Cells.Keys)
        {
            float px = sqrt3 * coord.X + sqrt3 * 0.5f * coord.Y;
            float py = 1.5f * coord.Y;
            if (px < minPx) minPx = px;
            if (px > maxPx) maxPx = px;
            if (py < minPy) minPy = py;
            if (py > maxPy) maxPy = py;
        }

        float rangeX = maxPx - minPx;
        float rangeY = maxPy - minPy;
        if (rangeX <= 0) rangeX = 1;
        if (rangeY <= 0) rangeY = 1;

        // 保持正方形内等比缩放(留 2px 边距)
        int margin = 4;
        int usable = RenderResolution - margin * 2;
        float scale = Mathf.Min(usable / rangeX, usable / rangeY);

        // 每个 cell 画一个小圆(半径 = scale * 0.45,让相邻 cell 的圆刚好接触)
        int dotR = Mathf.Max(2, (int)(scale * 0.45f));

        foreach (var kvp in _hexGrid.Cells)
        {
            var cell = kvp.Value;
            if (cell == null || !GodotObject.IsInstanceValid(cell)) continue;

            float px = sqrt3 * cell.GridPos.X + sqrt3 * 0.5f * cell.GridPos.Y;
            float py = 1.5f * cell.GridPos.Y;

            int ix = margin + (int)((px - minPx) * scale);
            int iy = margin + (int)((py - minPy) * scale);

            var color = GetCellColor(cell);
            // 画实心圆
            for (int dy = -dotR; dy <= dotR; dy++)
                for (int dx = -dotR; dx <= dotR; dx++)
                    if (dx * dx + dy * dy <= dotR * dotR)
                    {
                        int fx = ix + dx, fy = iy + dy;
                        if (fx >= 0 && fx < RenderResolution && fy >= 0 && fy < RenderResolution)
                            MapImage.SetPixel(fx, fy, color);
                    }
        }

        // 缓存转换参数供 Refresh / overlay 使用
        _pixMinX = minPx; _pixMinY = minPy;
        _pixScale = scale; _pixMargin = margin;

        FlushTexture();
    }

    // 缓存的 axial→pixel 转换参数
    private float _pixMinX, _pixMinY, _pixScale;
    private int _pixMargin;

    /// <summary>axial 坐标转小地图像素坐标</summary>
    private Vector2I AxialToMinimapPixel(Vector2I gridPos)
    {
        float sqrt3 = Mathf.Sqrt(3f);
        float px = sqrt3 * gridPos.X + sqrt3 * 0.5f * gridPos.Y;
        float py = 1.5f * gridPos.Y;
        int ix = _pixMargin + (int)((px - _pixMinX) * _pixScale);
        int iy = _pixMargin + (int)((py - _pixMinY) * _pixScale);
        return new Vector2I(ix, iy);
    }

    // ========================================
    // 公开接口
    // ========================================

    /// <summary>刷新单位位置和视野（每回合/每次移动后调用）</summary>
    public void Refresh()
    {
        if (!Initialized || _hexGrid == null) return;

        // 重绘地形底图
        BakeTerrain();

        // 叠加单位位置
        if (_combatMgr != null)
        {
            int unitDotR = Mathf.Max(3, (int)(_pixScale * 0.35f));

            void DrawUnitDot(Vector2I gridPos, Color color)
            {
                var p = AxialToMinimapPixel(gridPos);
                for (int dy = -unitDotR; dy <= unitDotR; dy++)
                    for (int dx = -unitDotR; dx <= unitDotR; dx++)
                        if (dx * dx + dy * dy <= unitDotR * unitDotR)
                        {
                            int fx = p.X + dx, fy = p.Y + dy;
                            if (fx >= 0 && fx < RenderResolution && fy >= 0 && fy < RenderResolution)
                                MapImage.SetPixel(fx, fy, color);
                        }
            }

            foreach (var unit in _combatMgr.PlayerUnits)
            {
                if (!GodotObject.IsInstanceValid(unit) || unit.CurrentHp <= 0) continue;
                DrawUnitDot(unit.GridPos, PlayerUnitColor);
            }

            foreach (var unit in _combatMgr.EnemyUnits)
            {
                if (!GodotObject.IsInstanceValid(unit) || unit.CurrentHp <= 0) continue;
                DrawUnitDot(unit.GridPos, EnemyUnitColor);
            }
        }

        FlushTexture();
        RequestOverlayRedraw();
    }

    /// <summary>更新相机视野数据（由 CombatSceneBase 每次相机移动后调用）</summary>
    public void UpdateViewport(Vector2 viewCenter, Vector2 viewExtent)
    {
        _viewCenter = viewCenter;
        _viewExtent = viewExtent;
        RequestOverlayRedraw();
    }

    // ========================================
    // 覆盖层绘制
    // ========================================

    protected override void DrawOverlay(Control overlay)
    {
        // 视野矩形框
        if (_viewExtent.X > 0 && _viewExtent.Y > 0)
        {
            float left = Mathf.Clamp(_viewCenter.X - _viewExtent.X, 0, _mapW - 1);
            float top = Mathf.Clamp(_viewCenter.Y - _viewExtent.Y, 0, _mapH - 1);
            float right = Mathf.Clamp(_viewCenter.X + _viewExtent.X, 0, _mapW - 1);
            float bottom = Mathf.Clamp(_viewCenter.Y + _viewExtent.Y, 0, _mapH - 1);

            var rect = new Rect2(new Vector2(left, top), new Vector2(right - left, bottom - top));
            DrawViewRect(overlay, rect, ViewportColor);
        }
    }

    // ========================================
    // 坐标转换
    // ========================================

    protected override Vector2 WorldToMinimap(Vector2 worldPos)
    {
        if (_worldWidth <= 0 || _worldHeight <= 0) return Vector2.Zero;
        return new Vector2(
            Mathf.Clamp(worldPos.X / _worldWidth * _mapW, 0, _mapW - 1),
            Mathf.Clamp(worldPos.Y / _worldHeight * _mapH, 0, _mapH - 1));
    }

    protected override Vector2 MinimapToWorld(Vector2 minimapPos)
    {
        return new Vector2(
            minimapPos.X / _mapW * _worldWidth,
            minimapPos.Y / _mapH * _worldHeight);
    }

    // ========================================
    // 地形颜色
    // ========================================

    private static Color GetCellColor(HexCell cell)
    {
        var terrainType = cell.Data?.terrainType ?? BattleCellData.TerrainType.Plains;
        return terrainType switch
        {
            BattleCellData.TerrainType.Plains => new Color(0.7f, 0.68f, 0.52f),
            BattleCellData.TerrainType.Grassland => new Color(0.4f, 0.62f, 0.3f),
            BattleCellData.TerrainType.Savanna => new Color(0.6f, 0.7f, 0.38f),
            BattleCellData.TerrainType.Forest => new Color(0.2f, 0.48f, 0.18f),
            BattleCellData.TerrainType.DenseForest => new Color(0.12f, 0.35f, 0.1f),
            BattleCellData.TerrainType.Hills => new Color(0.6f, 0.55f, 0.42f),
            BattleCellData.TerrainType.Mountain => new Color(0.4f, 0.38f, 0.35f),
            BattleCellData.TerrainType.ShallowWater => new Color(0.35f, 0.55f, 0.75f),
            BattleCellData.TerrainType.DeepWater => new Color(0.18f, 0.3f, 0.6f),
            BattleCellData.TerrainType.Swamp => new Color(0.35f, 0.42f, 0.25f),
            BattleCellData.TerrainType.Road => new Color(0.65f, 0.55f, 0.4f),
            BattleCellData.TerrainType.Sand => new Color(0.82f, 0.75f, 0.5f),
            BattleCellData.TerrainType.Snow => new Color(0.88f, 0.9f, 0.93f),
            BattleCellData.TerrainType.Wall => new Color(0.35f, 0.35f, 0.38f),
            BattleCellData.TerrainType.Ruins => new Color(0.5f, 0.45f, 0.4f),
            _ => new Color(0.5f, 0.5f, 0.5f),
        };
    }
}
