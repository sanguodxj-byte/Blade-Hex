// OverworldScene2D.Roads.cs
// 道路渲染 — 使用 Mesh Strip 方案（三角形条带 + UV 精确映射）
// Battle Brothers风格45°俯视：侧面厚度 + 投影阴影模拟斜视立体感
using Godot;
using BladeHex.Map;
using BladeHex.View.Map;

namespace BladeHex.Scenes.Overworld2d;

public partial class OverworldScene2D
{
    /// <summary>道路侧面厚度（像素），模拟45°俯视下道路截面</summary>
    private const float OverworldRoadSideThickness = 4.0f;

    /// <summary>河流岸堤侧面厚度（像素），模拟45°俯视下岸堤截面</summary>
    private const float OverworldRiverSideThickness = 3.0f;

    /// <summary>全局投影阴影偏移（像素）</summary>
    private static readonly Vector2 OverworldShadowOffset = new(2.0f, 4.0f);

    private BladeHex.View.Map.RoadRenderer? _roadRenderer;
    private RiverRenderer? _riverRenderer;

    private void RenderRoadsAndRivers()
    {
        // 道路 Mesh Strip（贝塞尔曲线 + 三角形条带 + 侧面厚度 + 投影阴影）
        if (_roadRenderer == null)
        {
            _roadRenderer = new BladeHex.View.Map.RoadRenderer { Name = "RoadRenderer" };
            _roadRenderer.RoadTexturePath = "res://assets/sprites/overworld/road_texture.png";
            _roadRenderer.RoadColor = new Color(0.95f, 0.82f, 0.55f); // 棕黄色调
            _roadRenderer.SideThickness = OverworldRoadSideThickness;
            _roadRenderer.SideColor = new Color(0.45f, 0.38f, 0.22f); // 深棕色侧面
            _roadRenderer.ShadowOffset = OverworldShadowOffset;
            _roadRenderer.ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.18f);
            AddChild(_roadRenderer);
            _roadRenderer.Initialize(_chunkManager);
            _roadRenderer.FullyBuilt = false;
            _roadRenderer.RebuildFromChunks();
        }

        // 河流 Mesh Strip（边缘过渡 + 主河流 + 侧面厚度 + 投影阴影）
        if (_riverRenderer == null)
        {
            _riverRenderer = new RiverRenderer { Name = "RiverRenderer" };
            _riverRenderer.RiverTexturePath = "res://assets/sprites/overworld/river_texture.png";
            _riverRenderer.RiverColor = new Color(1.0f, 1.0f, 1.0f, 0.9f); // 纹理原色，微透明
            _riverRenderer.SideThickness = OverworldRiverSideThickness;
            _riverRenderer.SideColor = new Color(0.05f, 0.15f, 0.35f); // 深蓝色岸堤侧面
            _riverRenderer.ShadowOffset = OverworldShadowOffset;
            _riverRenderer.ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.15f);
            AddChild(_riverRenderer);
            _riverRenderer.Initialize(_chunkManager);
            _riverRenderer.FullyBuilt = false;
            _riverRenderer.RebuildFromChunks();
        }
    }
}
