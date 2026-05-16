// HexOverworldRenderer3D.cs
// 3D 大地图六边形渲染器 — 替代旧 2D 版本
// MultiMeshInstance3D 按地形类型分桶批处理
// 世界坐标 UV shader 消除接缝，边缘羽化实现柔和过渡
using Godot;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.View.Map;

/// <summary>
/// 3D 大地图六边形批处理渲染器
/// 每种地形类型一个 MultiMeshInstance3D 桶
/// 使用 spatial shader 实现世界坐标连续 UV + 边缘羽化
/// </summary>
public partial class HexOverworldRenderer3D : Node3D
{
    // ========================================
    // 常量
    // ========================================

    /// <summary>3D 空间中六边形外径（世界单位）</summary>
    private const float HexRadius3D = 1.0f;

    /// <summary>2D 像素坐标到 3D 世界坐标的缩放因子</summary>
    /// <remarks>
    /// 原 2D 系统 HexSize=156px，3D 中 HexRadius=1.0
    /// 所以 scaleFactor = 1.0 / 156.0
    /// </remarks>
    private const float PixelToWorld = 1.0f / 156.0f;

    /// <summary>Shader 路径 — 程序化生成，不依赖纹理</summary>
    private const string ShaderPath = "res://src/assets/shaders/overworld_hex_procedural.gdshader";

    // ========================================
    // 桶结构
    // ========================================

    private class TerrainBucket3D
    {
        public MultiMeshInstance3D Instance = null!;
        public Dictionary<Vector2I, Vector3> TilePositions = new();
    }

    // ========================================
    // 字段
    // ========================================

    private Mesh? _sharedHexMesh;
    private Shader? _shader;
    private readonly Dictionary<int, TerrainBucket3D> _buckets = new();
    private readonly Dictionary<Vector2I, int> _tileTerrain = new();

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>初始化渲染器</summary>
    public void Initialize()
    {
        Name = "HexOverworldRenderer3D";
        _sharedHexMesh = CreateHexMesh3D();
        _shader = GD.Load<Shader>(ShaderPath);

        if (_shader == null)
            GD.PrintErr("[HexOverworldRenderer3D] 无法加载 shader: " + ShaderPath);
    }

    /// <summary>
    /// 加载一批瓦片到渲染器（chunk 加载时调用）
    /// </summary>
    public void LoadTiles(IEnumerable<HexOverworldTile> tiles)
    {
        var affectedTypes = new HashSet<int>();

        foreach (var tile in tiles)
        {
            int terrainType = (int)tile.Terrain;
            var tileCoord = tile.Coord;

            // 跳过已加载的
            if (_tileTerrain.ContainsKey(tileCoord))
                continue;

            _tileTerrain[tileCoord] = terrainType;

            // 像素坐标 → 3D 世界坐标（XZ 平面）
            Vector3 worldPos = new Vector3(
                tile.PixelPos.X * PixelToWorld,
                0,
                tile.PixelPos.Y * PixelToWorld
            );

            AddTileToBucket(terrainType, tileCoord, worldPos);
            affectedTypes.Add(terrainType);
        }

        foreach (var tt in affectedTypes)
            RebuildBucket(tt);
    }

    /// <summary>从 HexOverworldGrid 全量加载</summary>
    public void LoadFromGrid(HexOverworldGrid grid)
    {
        ClearAll();
        LoadTiles(grid.Tiles.Values);
        GD.Print($"[HexOverworldRenderer3D] 加载 {_tileTerrain.Count} 个瓦片, {_buckets.Count} 个地形桶");
    }

    /// <summary>清除所有渲染数据</summary>
    public void ClearAll()
    {
        foreach (var kvp in _buckets)
            kvp.Value.Instance.QueueFree();
        _buckets.Clear();
        _tileTerrain.Clear();
    }

    /// <summary>获取瓦片的 3D 世界坐标</summary>
    public Vector3 TileToWorld(HexOverworldTile tile)
    {
        return new Vector3(tile.PixelPos.X * PixelToWorld, 0, tile.PixelPos.Y * PixelToWorld);
    }

    // ========================================
    // 内部 — 桶管理
    // ========================================

    private void AddTileToBucket(int terrainType, Vector2I tileCoord, Vector3 worldPos)
    {
        if (!_buckets.TryGetValue(terrainType, out var bucket))
        {
            bucket = CreateBucket(terrainType);
            _buckets[terrainType] = bucket;
        }

        bucket.TilePositions[tileCoord] = worldPos;
    }

    private TerrainBucket3D CreateBucket(int terrainType)
    {
        var bucket = new TerrainBucket3D();
        bucket.Instance = new MultiMeshInstance3D();
        bucket.Instance.Name = $"Terrain3D_{((HexOverworldTile.TerrainType)terrainType)}";

        var profile = TerrainVisualRegistry.Get((HexOverworldTile.TerrainType)terrainType);

        // 创建 shader material — 程序化纹理（不再加载PNG）
        if (_shader != null)
        {
            var mat = new ShaderMaterial();
            mat.Shader = _shader;
            mat.SetShaderParameter("feather_width", 0.08f);
            mat.SetShaderParameter("noise_scale", 0.6f);
            mat.SetShaderParameter("warp_strength", 0.8f);
            mat.SetShaderParameter("ink_amount", 0.30f);
            mat.SetShaderParameter("ink_threshold", 0.42f);
            mat.SetShaderParameter("fog_amount", 0.0f);

            // 三色调色板（来自 TerrainVisualProfile）
            mat.SetShaderParameter("color_base", profile.DominantColor);
            mat.SetShaderParameter("color_dark", profile.PaletteDark);
            mat.SetShaderParameter("color_light", profile.PaletteLight);

            bucket.Instance.MaterialOverride = mat;
        }

        // 透明度支持（边缘羽化需要）
        bucket.Instance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        bucket.Instance.Transparency = 0.01f; // 触发 alpha 管线

        AddChild(bucket.Instance);
        return bucket;
    }

    private void RebuildBucket(int terrainType)
    {
        if (!_buckets.TryGetValue(terrainType, out var bucket))
            return;

        int count = bucket.TilePositions.Count;
        var mmi = bucket.Instance;

        if (count == 0)
        {
            mmi.Multimesh = null;
            return;
        }

        var mm = mmi.Multimesh;
        if (mm == null || mm.InstanceCount != count)
        {
            // 创建新 MultiMesh，先填充数据再赋值（减少闪烁）
            var newMm = new MultiMesh();
            newMm.Mesh = _sharedHexMesh;
            newMm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
            newMm.UseColors = false;
            newMm.InstanceCount = count;

            int idx = 0;
            foreach (var kvp in bucket.TilePositions)
            {
                newMm.SetInstanceTransform(idx, new Transform3D(Basis.Identity, kvp.Value));
                idx++;
            }

            // 数据就绪后一次性赋值
            mmi.Multimesh = newMm;
            return;
        }

        // 数量不变时直接更新 transform（零闪烁）
        int i = 0;
        foreach (var kvp in bucket.TilePositions)
        {
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, kvp.Value));
            i++;
        }
    }

    // ========================================
    // 纹理加载
    // ========================================

    private static Texture2D? TryLoadTexture(TerrainVisualProfile profile)
    {
        // 优先加载 overworld 目录
        string path = $"{HexOverworldTile.OverworldTextureBasePath}/{profile.OverworldKey}_0.png";
        if (ResourceLoader.Exists(path))
            return GD.Load<Texture2D>(path);

        // 映射地形到测试纹理
        string testKey = MapToTestTexture(profile.Terrain);
        if (!string.IsNullOrEmpty(testKey))
        {
            string testPath = $"res://src/assets/tiles/overworld_test/{testKey}.png";
            if (ResourceLoader.Exists(testPath))
                return GD.Load<Texture2D>(testPath);
        }

        return null;
    }

    /// <summary>将地形类型映射到 4 种测试纹理之一</summary>
    private static string MapToTestTexture(HexOverworldTile.TerrainType terrain)
    {
        return terrain switch
        {
            // 草地系 → test_grassland
            HexOverworldTile.TerrainType.Grassland => "test_grassland",
            HexOverworldTile.TerrainType.Forest => "test_grassland",
            HexOverworldTile.TerrainType.DenseForest => "test_grassland",
            HexOverworldTile.TerrainType.Jungle => "test_grassland",
            HexOverworldTile.TerrainType.Taiga => "test_grassland",
            HexOverworldTile.TerrainType.Swamp => "test_grassland",
            HexOverworldTile.TerrainType.Bog => "test_grassland",

            // 泥土系 → test_dirt_plains
            HexOverworldTile.TerrainType.Plains => "test_dirt_plains",
            HexOverworldTile.TerrainType.Hills => "test_dirt_plains",
            HexOverworldTile.TerrainType.Rocky => "test_dirt_plains",
            HexOverworldTile.TerrainType.Mountain => "test_dirt_plains",
            HexOverworldTile.TerrainType.Road => "test_dirt_plains",

            // 雪地系 → test_snow
            HexOverworldTile.TerrainType.Snow => "test_snow",
            HexOverworldTile.TerrainType.Ice => "test_snow",
            HexOverworldTile.TerrainType.MountainSnow => "test_snow",

            // 沙地系 → test_sand
            HexOverworldTile.TerrainType.Sand => "test_sand",
            HexOverworldTile.TerrainType.Savanna => "test_sand",
            HexOverworldTile.TerrainType.Wasteland => "test_sand",

            // 水域 → 无纹理，用纯色
            HexOverworldTile.TerrainType.DeepWater => "",
            HexOverworldTile.TerrainType.ShallowWater => "",
            HexOverworldTile.TerrainType.River => "",

            _ => "test_dirt_plains",
        };
    }

    // ========================================
    // Hex Mesh 创建
    // ========================================

    private static ArrayMesh CreateHexMesh3D()
    {
        float R = HexRadius3D;

        // Flat-top hex 顶点（XZ 平面，Y=0）
        var verts = new Vector3[7];
        verts[0] = Vector3.Zero;
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.DegToRad(60.0f * i);
            verts[i + 1] = new Vector3(R * Mathf.Cos(angle), 0, R * Mathf.Sin(angle));
        }

        // UV：用于 SDF 裁剪（hex 局部坐标映射到 0~1）
        var uvs = new Vector2[7];
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.DegToRad(60.0f * i);
            uvs[i + 1] = new Vector2(0.5f + 0.5f * Mathf.Cos(angle), 0.5f + 0.5f * Mathf.Sin(angle));
        }

        // 三角形扇
        var indices = new int[18];
        for (int i = 0; i < 6; i++)
        {
            indices[i * 3] = 0;
            indices[i * 3 + 1] = i + 1;
            indices[i * 3 + 2] = (i + 1) % 6 + 1;
        }

        var normals = new Vector3[7];
        for (int i = 0; i < 7; i++)
            normals[i] = Vector3.Up;

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.ResourceName = "HexOverworld3DMesh";
        return mesh;
    }
}
