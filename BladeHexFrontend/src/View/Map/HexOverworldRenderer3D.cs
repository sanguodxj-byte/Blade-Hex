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

    /// <summary>Shader 路径 — 纹理采样 + stochastic tiling 消除重复</summary>
    private const string TexturedShaderPath = "res://src/assets/shaders/overworld_hex_textured.gdshader";

    /// <summary>Shader 路径 — 程序化回退（无纹理时使用）</summary>
    private const string ProceduralShaderPath = "res://src/assets/shaders/overworld_hex_procedural.gdshader";

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
    private Shader? _texturedShader;
    private Shader? _proceduralShader;
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
        _texturedShader = GD.Load<Shader>(TexturedShaderPath);
        _proceduralShader = GD.Load<Shader>(ProceduralShaderPath);

        if (_texturedShader == null)
            GD.PrintErr("[HexOverworldRenderer3D] 无法加载纹理 shader: " + TexturedShaderPath);
        if (_proceduralShader == null)
            GD.PrintErr("[HexOverworldRenderer3D] 无法加载程序化 shader: " + ProceduralShaderPath);
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

            // 像素坐标 → 3D 世界坐标（XZ 平面）+ 高程 Y 偏移
            float elevY = GetElevationY(tile);
            Vector3 worldPos = new Vector3(
                tile.PixelPos.X * PixelToWorld,
                elevY,
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

    /// <summary>
    /// 更新太阳方向 uniform（每帧由场景调用，驱动 hillshade 明暗变化）。
    /// sunDir 是归一化的从地面指向太阳的方向向量。
    /// </summary>
    public void UpdateSunDirection(Vector3 sunDir)
    {
        foreach (var kvp in _buckets)
        {
            if (kvp.Value.Instance.MaterialOverride is ShaderMaterial mat)
            {
                mat.SetShaderParameter("sun_direction", sunDir);
            }
        }
    }

    /// <summary>获取瓦片的 3D 世界坐标（含高程）</summary>
    public Vector3 TileToWorld(HexOverworldTile tile)
    {
        float elevY = GetElevationY(tile);
        return new Vector3(tile.PixelPos.X * PixelToWorld, elevY, tile.PixelPos.Y * PixelToWorld);
    }

    // ========================================
    // 高程系统
    // ========================================

    /// <summary>高程缩放因子 — 控制地形起伏的视觉幅度</summary>
    private const float ElevationScale = 0.8f;

    /// <summary>高程基准偏移 — 让平均地形（elevation≈0.5）在 Y=0 附近</summary>
    private const float ElevationBaseline = 0.5f;

    /// <summary>
    /// 根据 tile 的 Elevation 和地形类型计算 3D 世界 Y 坐标。
    /// Elevation 范围 [0, 1]，映射到 Y 坐标 [-ElevationScale/2, +ElevationScale/2]。
    /// 水域强制压低，山地额外抬高。
    /// </summary>
    private static float GetElevationY(HexOverworldTile tile)
    {
        float baseElev = (tile.Elevation - ElevationBaseline) * ElevationScale;

        // 地形类型微调：让视觉层次更明显
        float terrainBonus = tile.Terrain switch
        {
            HexOverworldTile.TerrainType.Mountain => 0.45f,
            HexOverworldTile.TerrainType.MountainSnow => 0.55f,
            HexOverworldTile.TerrainType.Hills => 0.12f,
            HexOverworldTile.TerrainType.Rocky => 0.08f,
            HexOverworldTile.TerrainType.DeepWater => -0.18f,
            HexOverworldTile.TerrainType.ShallowWater => -0.10f,
            HexOverworldTile.TerrainType.River => -0.08f,
            HexOverworldTile.TerrainType.Swamp => -0.05f,
            HexOverworldTile.TerrainType.Bog => -0.04f,
            _ => 0.0f,
        };

        return baseElev + terrainBonus;
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

        // 尝试加载纹理资产
        var texture = TryLoadTexture(profile);

        if (texture != null && _texturedShader != null)
        {
            // ===== 纹理模式：stochastic tiling 消除重复 =====
            var mat = new ShaderMaterial();
            mat.Shader = _texturedShader;
            mat.SetShaderParameter("terrain_texture", texture);
            mat.SetShaderParameter("feather_width", 0.03f);

            // 纹理缩放：不同地形纹理密度不同
            float texScale = profile.PatternType switch
            {
                1 => 0.35f, // 森林：纹理稍密
                2 => 0.25f, // 水域：大尺度波纹
                3 => 0.30f, // 山地：中等
                4 => 0.20f, // 沙漠：大尺度沙丘
                5 => 0.25f, // 雪地：平整
                6 => 0.40f, // 道路：密
                7 => 0.30f, // 沼泽：中等
                _ => 0.30f, // 平原/草地
            };
            mat.SetShaderParameter("texture_scale", texScale);

            // Stochastic 混合锐度
            mat.SetShaderParameter("blend_sharpness", 8.0f);
            // 色调微偏移（打破重复）
            mat.SetShaderParameter("color_variance", 0.04f);

            // 墨线叠加（保持画风统一）
            float inkAmount = profile.PatternType switch
            {
                1 => 0.35f, // 森林
                2 => 0.08f, // 水域
                3 => 0.40f, // 山地
                4 => 0.05f, // 沙漠
                5 => 0.0f,  // 雪地
                6 => 0.15f, // 道路
                7 => 0.30f, // 沼泽
                _ => 0.20f, // 平原
            };
            mat.SetShaderParameter("ink_amount", inkAmount);
            mat.SetShaderParameter("ink_threshold", 0.42f);
            mat.SetShaderParameter("ink_color", profile.PaletteDark);
            mat.SetShaderParameter("fog_amount", 0.0f);

            // 色调校正（让纹理向目标调色板靠拢）
            mat.SetShaderParameter("tint_color", profile.DominantColor);
            mat.SetShaderParameter("tint_strength", 0.15f);

            bucket.Instance.MaterialOverride = mat;
        }
        else if (_proceduralShader != null)
        {
            // ===== 程序化回退：无纹理时使用原有 noise 方案 =====
            var mat = new ShaderMaterial();
            mat.Shader = _proceduralShader;
            mat.SetShaderParameter("feather_width", 0.03f);

            float noiseScale = profile.PatternType switch
            {
                1 => 0.9f,
                2 => 0.7f,
                3 => 0.5f,
                4 => 0.4f,
                5 => 0.3f,
                6 => 1.2f,
                7 => 0.8f,
                _ => 0.6f,
            };
            mat.SetShaderParameter("noise_scale", noiseScale);
            mat.SetShaderParameter("warp_strength", 0.8f);

            float inkAmount = profile.PatternType switch
            {
                1 => 0.45f,
                2 => 0.10f,
                3 => 0.50f,
                4 => 0.05f,
                5 => 0.0f,
                6 => 0.20f,
                7 => 0.40f,
                _ => 0.30f,
            };
            mat.SetShaderParameter("ink_amount", inkAmount);
            mat.SetShaderParameter("ink_threshold", 0.42f);
            mat.SetShaderParameter("fog_amount", 0.0f);

            mat.SetShaderParameter("color_base", profile.DominantColor);
            mat.SetShaderParameter("color_dark", profile.PaletteDark);
            mat.SetShaderParameter("color_light", profile.PaletteLight);
            mat.SetShaderParameter("pattern_type", profile.PatternType);

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

    /// <summary>裙边向下延伸的深度（世界单位）</summary>
    private const float SkirtDepth = 1.2f;

    private static ArrayMesh CreateHexMesh3D()
    {
        // 稍微放大 hex（1.02×）让相邻 hex 边缘重叠，消除同高度接缝
        float R = HexRadius3D * 1.02f;

        // ========================================
        // 顶面：7 顶点（中心 + 6 角），6 三角形
        // 裙边：每条边 2 个三角形 × 6 边 = 12 三角形，12 个额外顶点
        //       （顶部边缘顶点复制一份 + 底部边缘顶点）
        // 总计：7 + 12 = 19 顶点，18 + 36 = 54 索引
        // ========================================

        int topVertCount = 7;
        int totalVerts = topVertCount + 24;
        int topIdxCount = 18;
        int skirtIdxCount = 36;
        int totalIdx = topIdxCount + skirtIdxCount;

        var verts = new Vector3[totalVerts];
        var uvs = new Vector2[totalVerts];
        var normals = new Vector3[totalVerts];
        var indices = new int[totalIdx];

        // --- 顶面顶点 ---
        verts[0] = Vector3.Zero;
        uvs[0] = new Vector2(0.5f, 0.5f);
        normals[0] = Vector3.Up;

        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.DegToRad(60.0f * i);
            verts[i + 1] = new Vector3(R * Mathf.Cos(angle), 0, R * Mathf.Sin(angle));
            uvs[i + 1] = new Vector2(0.5f + 0.5f * Mathf.Cos(angle), 0.5f + 0.5f * Mathf.Sin(angle));
            normals[i + 1] = Vector3.Up;
        }

        // --- 顶面三角形 ---
        for (int i = 0; i < 6; i++)
        {
            indices[i * 3] = 0;
            indices[i * 3 + 1] = i + 1;
            indices[i * 3 + 2] = (i + 1) % 6 + 1;
        }

        // --- 裙边顶点和三角形 ---
        int vIdx = topVertCount;
        int iIdx = topIdxCount;

        for (int i = 0; i < 6; i++)
        {
            int nextI = (i + 1) % 6;

            // 上边缘两个顶点（复制顶面边缘，但法线朝外）
            Vector3 topLeft = verts[i + 1];
            Vector3 topRight = verts[nextI + 1];
            // 下边缘两个顶点
            Vector3 botLeft = topLeft + new Vector3(0, -SkirtDepth, 0);
            Vector3 botRight = topRight + new Vector3(0, -SkirtDepth, 0);

            // 法线：朝外（边的中点方向）
            Vector3 edgeMid = (topLeft + topRight) * 0.5f;
            Vector3 outNormal = new Vector3(edgeMid.X, 0, edgeMid.Z).Normalized();

            // 4 个顶点
            int v0 = vIdx; // top-left
            int v1 = vIdx + 1; // top-right
            int v2 = vIdx + 2; // bot-left
            int v3 = vIdx + 3; // bot-right

            verts[v0] = topLeft;
            verts[v1] = topRight;
            verts[v2] = botLeft;
            verts[v3] = botRight;

            // UV 标记裙边：UV.y > 1.0；UV.x 编码深度（0=顶部，1=底部）
            uvs[v0] = new Vector2(0.0f, 1.5f);
            uvs[v1] = new Vector2(0.0f, 1.5f);
            uvs[v2] = new Vector2(1.0f, 1.5f);
            uvs[v3] = new Vector2(1.0f, 1.5f);

            normals[v0] = outNormal;
            normals[v1] = outNormal;
            normals[v2] = outNormal;
            normals[v3] = outNormal;

            // 两个三角形（顺时针）
            indices[iIdx++] = v0;
            indices[iIdx++] = v2;
            indices[iIdx++] = v1;
            indices[iIdx++] = v1;
            indices[iIdx++] = v2;
            indices[iIdx++] = v3;

            vIdx += 4;
        }

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
