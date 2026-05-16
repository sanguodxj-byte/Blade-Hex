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
    protected override int MapPixelWidth => _mapW;
    protected override int MapPixelHeight => _mapH;
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
        _mapW = mapWidth;
        _mapH = mapHeight;

        // 世界尺寸 = 格数 × 格间距
        _worldWidth = mapWidth * HexUtils.HorizontalSpacing;
        _worldHeight = mapHeight * HexUtils.VerticalSpacing;

        BuildBaseUI();
        BakeTerrain();
        Initialized = true;
    }

    // ========================================
    // 地形烘焙（一次性，战斗地形不变）
    // ========================================

    private void BakeTerrain()
    {
        if (_hexGrid == null) return;

        MapImage.Fill(DefaultBgColor);

        foreach (var kvp in _hexGrid.Cells)
        {
            var cell = kvp.Value;
            if (cell == null || !GodotObject.IsInstanceValid(cell)) continue;

            int x = cell.GridPos.X;
            int y = cell.GridPos.Y;
            if (x < 0 || x >= _mapW || y < 0 || y >= _mapH) continue;

            MapImage.SetPixel(x, y, GetCellColor(cell));
        }

        FlushTexture();
    }

    // ========================================
    // 公开接口
    // ========================================

    /// <summary>刷新单位位置和视野（每回合/每次移动后调用）</summary>
    public void Refresh()
    {
        if (!Initialized || _hexGrid == null) return;

        // 重绘地形底图（保持地形不变）
        BakeTerrain();

        // 叠加单位位置
        if (_combatMgr != null)
        {
            foreach (var unit in _combatMgr.PlayerUnits)
            {
                if (!GodotObject.IsInstanceValid(unit) || unit.CurrentHp <= 0) continue;
                int x = unit.GridPos.X, y = unit.GridPos.Y;
                if (x >= 0 && x < _mapW && y >= 0 && y < _mapH)
                    MapImage.SetPixel(x, y, PlayerUnitColor);
            }

            foreach (var unit in _combatMgr.EnemyUnits)
            {
                if (!GodotObject.IsInstanceValid(unit) || unit.CurrentHp <= 0) continue;
                int x = unit.GridPos.X, y = unit.GridPos.Y;
                if (x >= 0 && x < _mapW && y >= 0 && y < _mapH)
                    MapImage.SetPixel(x, y, EnemyUnitColor);
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
