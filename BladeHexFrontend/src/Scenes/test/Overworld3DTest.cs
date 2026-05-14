// Overworld3DTest.cs
// 3D 大地图渲染原型 — 加载真实地图数据测试
// 使用 HexOverworldGenerator 生成地图，HexOverworldRenderer3D 渲染
// 按 1-4 切换纹理模式，WASD 平移，滚轮缩放
using Godot;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.View.Map;

namespace BladeHex.Test;

/// <summary>
/// 3D 大地图渲染完整测试 — 加载真实生成的地图数据
/// </summary>
public partial class Overworld3DTest : Node3D
{
    private OverworldCamera3D? _camera;
    private HexOverworldRenderer3D? _renderer;
    private HexOverworldGrid? _grid;
    private Label? _infoLabel;

    public override void _Ready()
    {
        // 生成地图
        var gen = new HexOverworldGenerator();
        _grid = gen.Generate(64, 48, 12345);

        // 3D 渲染器
        _renderer = new HexOverworldRenderer3D();
        AddChild(_renderer);
        _renderer.Initialize();
        _renderer.LoadFromGrid(_grid);

        // 相机
        _camera = new OverworldCamera3D();
        AddChild(_camera);

        // 聚焦到地图中心
        var center = _grid.GetCenterPixel();
        float cx = center.X / 156.0f;
        float cz = center.Y / 156.0f;
        _camera.FocusOnXZ(cx, cz);

        // 光照
        SetupLight();

        // UI
        SetupUI();

        GD.Print($"[Overworld3DTest] 地图 64×48 已加载, WASD 平移, 滚轮缩放");
    }

    private void SetupLight()
    {
        var light = new DirectionalLight3D();
        light.RotationDegrees = new Vector3(-45, -30, 0);
        light.LightEnergy = 0.8f;
        light.ShadowEnabled = false;
        AddChild(light);

        var env = new WorldEnvironment();
        var envRes = new Godot.Environment();
        envRes.BackgroundMode = Godot.Environment.BGMode.Color;
        envRes.BackgroundColor = new Color(0.12f, 0.15f, 0.20f);
        envRes.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        envRes.AmbientLightColor = new Color(0.7f, 0.7f, 0.72f);
        envRes.AmbientLightEnergy = 0.4f;
        env.Environment = envRes;
        AddChild(env);
    }

    private void SetupUI()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        _infoLabel = new Label();
        _infoLabel.Position = new Vector2(16, 16);
        _infoLabel.AddThemeColorOverride("font_color", Colors.White);
        _infoLabel.AddThemeFontSizeOverride("font_size", 16);
        _infoLabel.Text = "3D Overworld Test | WASD 平移 | 滚轮缩放\n" +
                          $"地图: 64×48 | 瓦片: {_grid?.TileCount() ?? 0}";
        canvas.AddChild(_infoLabel);
    }
}
