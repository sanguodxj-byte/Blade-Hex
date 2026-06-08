// Overworld2DTest.cs
// 2D 大地图渲染原型 — 硬编码多地形展示版
// 使用 HexOverworldGenerator 生成基础结构，然后强制按区域覆盖地形
// WASD 平移，滚轮缩放
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;

namespace BladeHex.Test;

/// <summary>
/// 2D 大地图渲染完整测试 — 硬编码多地形分区，确保截图包含河流/山地/森林/草原
/// </summary>
public partial class Overworld2DTest : Node2D
{
    private const int TestSeed   = 12345;
    private const int MapWidth   = 64;
    private const int MapHeight  = 48;

    // 相机初始缩放：1.0 可清晰看到精灵细节和山脉 Y 堆叠效果
    private const float InitialZoom = 1.0f;

    private OverworldCamera2D?    _camera;
    private HexOverworldRenderer2D? _renderer;
    private OverworldPropRenderer2D? _propRenderer;
    private HexOverworldGrid?     _grid;
    private Label?                _infoLabel;
    private Label?                _terrainLabel;

    public override void _Ready()
    {
        // ── 第1步：生成基础地图（噪声层/PixelPos/邻居关系） ─────────────
        var gen = new HexOverworldGenerator();
        _grid = gen.Generate(MapWidth, MapHeight, TestSeed);

        // ── 第2步：按坐标区域强制覆盖地形 ──────────────────────────────
        // 将 64 列分成 8 个纵向带，每带约 8 列，覆盖典型地形
        // 同时保留河流（IsRiver==true 的格子不覆盖，保持水系形态）
        ForceTerrainZones();

        // ── 第3步：渲染 ──────────────────────────────────────────────────
        _renderer = new HexOverworldRenderer2D();
        AddChild(_renderer);
        _renderer.Initialize();
        _renderer.LoadFromGrid(_grid);

        _propRenderer = new OverworldPropRenderer2D();
        AddChild(_propRenderer);
        _propRenderer.Initialize(TestSeed, _grid);
        _propRenderer.LoadPropsForTiles(_grid.Tiles.Values);

        // ── 第4步：相机对焦地图中心，缩放至 InitialZoom ─────────────────
        _camera = new OverworldCamera2D();
        _camera.BaseZoom = InitialZoom;
        AddChild(_camera);

        // 对焦到山脉区域中心（而非整个地图中心），确保山脉在视口内
        Vector2 focusTarget = GetMountainCenterPixel() ?? _grid.GetCenterPixel();
        _camera.FocusOnImmediate(focusTarget);

        // ── 第5步：统计并显示 ─────────────────────────────────────────────
        var counts = CountTerrains();
        PrintTerrainStats(counts);
        SetupUI(counts);

        GD.Print($"[Overworld2DTest] 硬编码多地形地图已加载 | 对焦={focusTarget} | Zoom={InitialZoom}");

        // ── 自动截图 ──────────────────────────────────────────────────────
        foreach (var arg in OS.GetCmdlineArgs())
        {
            if (arg == "--screenshot-test")
            {
                GD.Print("[Overworld2DTest] 检测到 --screenshot-test 参数，2秒后截图...");
                var timer = GetTree().CreateTimer(2.0f);
                timer.Timeout += () =>
                {
                    // 强制对焦到森林区域偏右中心，并将缩放拉近至 1.25f 以便审计细节
                    Vector2 forestFocus = GetForestCenterPixel() ?? _grid.GetCenterPixel();
                    forestFocus.X += 800.0f; // 往右偏移 800 像素以避开山脉，完美展示森林边缘向水域过渡的稀松带
                    if (_camera != null)
                    {
                        _camera.SetProcess(false); // 关掉平滑插值，防止其在下一帧重置缩放
                        _camera.Zoom = new Vector2(1.25f, 1.25f);
                        _camera.Position = forestFocus;
                    }

                    // 延迟一帧，等待 RenderingServer 完成画面提交后进行截图
                    Callable.From(() =>
                    {
                        var img = GetViewport().GetTexture().GetImage();
                        if (img != null)
                        {
                            string dir = "playability_screenshots";
                            var da = DirAccess.Open("res://");
                            if (!da.DirExists(dir)) da.MakeDir(dir);
                            string path = $"{dir}/overworld_prop_test.png";
                            var err = img.SavePng(path);
                            if (err == Error.Ok)
                                GD.Print($"[Overworld2DTest] 截图保存至: {path}");
                            else
                                GD.PrintErr($"[Overworld2DTest] 截图失败: {err}");
                        }
                        GetTree().Quit();
                    }).CallDeferred();
                };
                break;
            }
        }
    }

    // =========================================================================
    // 硬编码地形分区（归一化列坐标）
    // 山脉放在中央（t≈0.4~0.6）确保相机能看到
    // 从左到右：沙漠 → 热带草原 → 沼泽 → 丘陵 → 平原 → 草地
    //            → 密林 → 雪山 → 山地 → 森林 → 浅海 → 深水
    // =========================================================================
    private void ForceTerrainZones()
    {
        // 1. 探测 q 坐标实际范围
        int qMin = int.MaxValue, qMax = int.MinValue;
        foreach (var tile in _grid!.Tiles.Values)
        {
            if (tile.Coord.X < qMin) qMin = tile.Coord.X;
            if (tile.Coord.X > qMax) qMax = tile.Coord.X;
        }
        float qRange = Mathf.Max(qMax - qMin, 1);
        GD.Print($"[Overworld2DTest] q 坐标范围: {qMin} ~ {qMax}（共 {qRange} 格）");

        // 2. 按归一化列位置强制设置地形 — 山脉居中
        foreach (var tile in _grid.Tiles.Values)
        {
            // 跳过生成器已生成的河流（保留水系外观）
            if (tile.IsRiver) continue;

            float t = (tile.Coord.X - qMin) / qRange; // 0.0 ~ 1.0

            HexOverworldTile.TerrainType forced = t switch
            {
                < 0.08f => HexOverworldTile.TerrainType.Sand,
                < 0.15f => HexOverworldTile.TerrainType.Savanna,
                < 0.22f => HexOverworldTile.TerrainType.Swamp,
                < 0.29f => HexOverworldTile.TerrainType.Hills,
                < 0.36f => HexOverworldTile.TerrainType.Plains,
                < 0.42f => HexOverworldTile.TerrainType.Grassland,
                // 中央区域：山脉
                < 0.50f => HexOverworldTile.TerrainType.Mountain,
                < 0.58f => HexOverworldTile.TerrainType.MountainSnow,
                < 0.66f => HexOverworldTile.TerrainType.DenseForest,
                < 0.76f => HexOverworldTile.TerrainType.Forest,
                < 0.86f => HexOverworldTile.TerrainType.ShallowWater,
                _       => HexOverworldTile.TerrainType.DeepWater,
            };

            tile.SetTerrain(forced);
            tile.UpdateTerrainProperties();
        }
    }

    /// <summary>计算山脉地形区域的中心像素坐标，用于相机对焦</summary>
    private Vector2? GetMountainCenterPixel()
    {
        if (_grid == null) return null;
        float sumX = 0, sumY = 0;
        int count = 0;
        foreach (var tile in _grid.Tiles.Values)
        {
            if (tile.Terrain == HexOverworldTile.TerrainType.Mountain ||
                tile.Terrain == HexOverworldTile.TerrainType.MountainSnow)
            {
                sumX += tile.PixelPos.X;
                sumY += tile.PixelPos.Y;
                count++;
            }
        }
        if (count == 0) return null;
        var center = new Vector2(sumX / count, sumY / count);
        GD.Print($"[Overworld2DTest] 山脉中心: {center} (共 {count} 格)");
        return center;
    }


    private Dictionary<HexOverworldTile.TerrainType, int> CountTerrains()
    {
        var counts = new Dictionary<HexOverworldTile.TerrainType, int>();
        foreach (var tile in _grid!.Tiles.Values)
        {
            if (!counts.ContainsKey(tile.Terrain)) counts[tile.Terrain] = 0;
            counts[tile.Terrain]++;
        }
        return counts;
    }

    private void PrintTerrainStats(Dictionary<HexOverworldTile.TerrainType, int> counts)
    {
        var sb = new System.Text.StringBuilder($"[Overworld2DTest] 硬编码地形分布:\n");
        foreach (var kv in counts)
            sb.AppendLine($"  {kv.Key,-20} {kv.Value,5} 格");
        GD.Print(sb.ToString());
    }

    private void SetupUI(Dictionary<HexOverworldTile.TerrainType, int> counts)
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        _infoLabel = new Label();
        _infoLabel.Position = new Vector2(16, 16);
        _infoLabel.AddThemeColorOverride("font_color", Colors.White);
        _infoLabel.AddThemeFontSizeOverride("font_size", 16);
        _infoLabel.Text = $"2D Overworld Test (硬编码多地形) | WASD 平移 | 滚轮缩放\n" +
                          $"地图: {MapWidth}×{MapHeight} | 瓦片: {_grid?.TileCount() ?? 0} | Zoom: {InitialZoom}";
        canvas.AddChild(_infoLabel);

        _terrainLabel = new Label();
        _terrainLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _terrainLabel.Position = new Vector2(-310, 16);
        _terrainLabel.Size = new Vector2(290, 500);
        _terrainLabel.AnchorLeft = 1.0f;
        _terrainLabel.AnchorRight = 1.0f;
        _terrainLabel.AddThemeColorOverride("font_color", new Color(1, 1, 0.6f));
        _terrainLabel.AddThemeFontSizeOverride("font_size", 13);
        var sb = new System.Text.StringBuilder("地形分布:\n");
        foreach (var kv in counts)
            if (kv.Value > 0)
                sb.AppendLine($"{kv.Key}: {kv.Value}");
        _terrainLabel.Text = sb.ToString();
        canvas.AddChild(_terrainLabel);
    }

    public override void _Process(double delta)
    {
        if (_infoLabel != null && _camera != null)
        {
            int propCount = _propRenderer?.PropCount ?? 0;
            _infoLabel.Text = $"2D Overworld Test (硬编码多地形) | WASD 平移 | 滚轮缩放\n" +
                              $"地图: {MapWidth}×{MapHeight} | 瓦片: {_grid?.TileCount() ?? 0}\n" +
                              $"Props: {propCount} | Zoom: {_camera.Zoom.X:F2}";
        }
    }

    /// <summary>计算森林地形区域的中心像素坐标，用于相机对焦</summary>
    private Vector2? GetForestCenterPixel()
    {
        if (_grid == null) return null;
        float sumX = 0, sumY = 0;
        int count = 0;
        foreach (var tile in _grid.Tiles.Values)
        {
            if (tile.Terrain == HexOverworldTile.TerrainType.Forest ||
                tile.Terrain == HexOverworldTile.TerrainType.DenseForest)
            {
                sumX += tile.PixelPos.X;
                sumY += tile.PixelPos.Y;
                count++;
            }
        }
        if (count == 0) return null;
        var center = new Vector2(sumX / count, sumY / count);
        GD.Print($"[Overworld2DTest] 森林中心: {center} (共 {count} 格)");
        return center;
    }
}
