// OverworldPropRenderer.cs
// 大地图场景物体渲染器 — MultiMesh 批处理版本
// 按 prop 类型分桶，每种类型一个 MultiMeshInstance3D（QuadMesh billboard）
// 支持增量加载、LOD（缩小时隐藏）、正确的遮挡排序
using Godot;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.View.Map;

/// <summary>
/// 大地图 prop 渲染器（MultiMesh 批处理版本）。
/// 
/// 设计：
/// - 每种 prop 类型（oak_tree, rock_small 等）一个 MultiMeshInstance3D
/// - 每个实例只是一个 Transform3D（位置+缩放+翻转），不创建独立节点
/// - 相机缩放超过阈值时整体隐藏（LOD）
/// - 遮挡排序：Position.Z 越大（画面越下方）越靠前渲染，通过 Z-buffer 自然遮挡
/// 
/// 性能：10 万个 prop 只需要 ~30 个 MultiMesh 节点（按类型分桶）
/// </summary>
public partial class OverworldPropRenderer : Node3D
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>像素到 3D 世界坐标的缩放因子</summary>
    private const float PixelToWorld = 1.0f / 156.0f;

    /// <summary>prop sprite 的世界缩放（像素 → 世界单位）。1024px 精灵 × 0.0006 ≈ 0.6 世界单位（约 hex 的 1/3）</summary>
    private const float SpriteWorldScale = 0.0006f;

    /// <summary>prop 底部离地面的高度（避免 Z-fighting）</summary>
    private const float BaseElevation = 0.02f;

    /// <summary>相机正交 Size 超过此值时隐藏 props（LOD）</summary>
    private const float LodHideThreshold = 20.0f;

    /// <summary>相机正交 Size 低于此值时完全显示 props</summary>
    private const float LodShowThreshold = 16.0f;

    // ========================================
    // 桶结构
    // ========================================

    private class PropBucket
    {
        public string PropId = "";
        public MultiMeshInstance3D Instance = null!;
        public List<Transform3D> Transforms = new();
        public bool Dirty = false; // 需要重建 MultiMesh
        public float SpriteHeight; // 纹理高度（像素）
        public float SpriteWidth;  // 纹理宽度（像素）
    }

    // ========================================
    // 状态
    // ========================================

    private readonly Dictionary<string, PropBucket> _buckets = new();
    private readonly HashSet<Vector2I> _loadedTiles = new();
    private int _worldSeed;
    private bool _visible = true;

    // ========================================
    // 初始化
    // ========================================

    /// <summary>初始化渲染器</summary>
    public void Initialize(int worldSeed)
    {
        Name = "OverworldPropRenderer";
        _worldSeed = worldSeed;
    }

    // ========================================
    // 增量加载
    // ========================================

    /// <summary>
    /// 为一批 tile 生成并加载 props 到 MultiMesh 桶。
    /// </summary>
    public void LoadPropsForTiles(IEnumerable<HexOverworldTile> tiles)
    {
        bool anyNew = false;

        foreach (var tile in tiles)
        {
            if (_loadedTiles.Contains(tile.Coord)) continue;
            _loadedTiles.Add(tile.Coord);

            var props = OverworldPropScatter.Generate(tile.Coord, tile.Terrain, _worldSeed);
            if (props.Count == 0) continue;

            float tileElevY = GetTileElevationY(tile);
            foreach (var propData in props)
            {
                AddPropToBucket(propData, tile.PixelPos, tileElevY);
                anyNew = true;
            }
        }

        if (anyNew)
            RebuildDirtyBuckets();
    }

    /// <summary>卸载指定 tile 的 props（需要重建桶）</summary>
    public void UnloadPropsForTile(Vector2I tileCoord)
    {
        // MultiMesh 模式下卸载单个 tile 比较昂贵（需要重建整个桶）
        // 实际使用中大地图 props 通常不卸载（内存可控），
        // 如果需要卸载，调用 ClearAll 后重新加载可见区域
        _loadedTiles.Remove(tileCoord);
    }

    /// <summary>清除所有 props</summary>
    public void ClearAll()
    {
        foreach (var kvp in _buckets)
        {
            kvp.Value.Transforms.Clear();
            kvp.Value.Instance.QueueFree();
        }
        _buckets.Clear();
        _loadedTiles.Clear();
    }

    /// <summary>当前已加载的 prop 总数</summary>
    public int PropCount
    {
        get
        {
            int count = 0;
            foreach (var kvp in _buckets) count += kvp.Value.Transforms.Count;
            return count;
        }
    }

    // ========================================
    // LOD：根据相机缩放显示/隐藏
    // ========================================

    /// <summary>
    /// 每帧调用，根据相机正交 Size 控制 prop 层可见性。
    /// </summary>
    public void UpdateLOD(float cameraOrthoSize)
    {
        if (cameraOrthoSize > LodHideThreshold)
        {
            if (_visible) { Visible = false; _visible = false; }
        }
        else if (cameraOrthoSize < LodShowThreshold)
        {
            if (!_visible) { Visible = true; _visible = true; }
        }
    }

    // ========================================
    // 内部：桶管理
    // ========================================

    private void AddPropToBucket(OverworldPropData propData, Vector2 tilePixelPos, float tileElevY = 0.0f)
    {
        if (!_buckets.TryGetValue(propData.PropId, out var bucket))
        {
            bucket = CreateBucket(propData.PropId);
            _buckets[propData.PropId] = bucket;
        }

        // 计算 3D 世界位置（底部锚点）
        float pixelX = tilePixelPos.X + propData.PixelOffset.X;
        float pixelY = tilePixelPos.Y + propData.PixelOffset.Y;
        float worldX = pixelX * PixelToWorld;
        float worldZ = pixelY * PixelToWorld;

        // 缩放（X 可能为负 = 水平翻转）
        float scaleX = propData.FlipH ? -propData.Scale : propData.Scale;
        float scaleY = propData.Scale;

        // Transform：位置在底部，QuadMesh 的 pivot 在中心所以需要 Y 偏移
        float worldHeight = bucket.SpriteHeight * SpriteWorldScale * propData.Scale;
        float yPos = tileElevY + BaseElevation + worldHeight * 0.5f;

        var basis = new Basis(
            new Vector3(scaleX, 0, 0),
            new Vector3(0, scaleY, 0),
            new Vector3(0, 0, 1));
        var transform = new Transform3D(basis, new Vector3(worldX, yPos, worldZ));

        bucket.Transforms.Add(transform);
        bucket.Dirty = true;
    }

    /// <summary>计算 tile 的高程 Y（与 HexOverworldRenderer3D 一致）</summary>
    private static float GetTileElevationY(HexOverworldTile tile)
    {
        const float ElevationScale = 0.8f;
        const float ElevationBaseline = 0.5f;
        float baseElev = (tile.Elevation - ElevationBaseline) * ElevationScale;
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

    private PropBucket CreateBucket(string propId)
    {
        var texture = OverworldPropRegistry.GetTexture(propId);
        float texW = texture.GetWidth();
        float texH = texture.GetHeight();

        // 创建 QuadMesh（尺寸 = 纹理像素 × SpriteWorldScale）
        float meshW = texW * SpriteWorldScale;
        float meshH = texH * SpriteWorldScale;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(meshW, meshH);
        // Billboard 朝向：法线朝 Z+（相机方向）
        quadMesh.Orientation = PlaneMesh.OrientationEnum.Z;

        // 材质：StandardMaterial3D + billboard + mipmap + alpha prepass
        var mat = new StandardMaterial3D();
        mat.AlbedoTexture = texture;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY;
        mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
        mat.AlphaScissorThreshold = 0.5f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        // 关键：不写入深度时使用 render priority 排序
        // 但我们用 alpha scissor（不透明预通道），所以 Z-buffer 正常工作
        // Z 越大（画面越下方）离相机越近 → 自然遮挡 Z 小的物体 ✓

        var instance = new MultiMeshInstance3D();
        instance.Name = $"PropBucket_{propId}";
        instance.MaterialOverride = mat;
        instance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(instance);

        return new PropBucket
        {
            PropId = propId,
            Instance = instance,
            SpriteHeight = texH,
            SpriteWidth = texW,
        };
    }

    private void RebuildDirtyBuckets()
    {
        foreach (var kvp in _buckets)
        {
            var bucket = kvp.Value;
            if (!bucket.Dirty) continue;
            bucket.Dirty = false;

            int count = bucket.Transforms.Count;
            if (count == 0)
            {
                bucket.Instance.Multimesh = null;
                continue;
            }

            // 创建 QuadMesh（每次重建确保尺寸正确）
            float meshW = bucket.SpriteWidth * SpriteWorldScale;
            float meshH = bucket.SpriteHeight * SpriteWorldScale;
            var quadMesh = new QuadMesh();
            quadMesh.Size = new Vector2(meshW, meshH);
            quadMesh.Orientation = PlaneMesh.OrientationEnum.Z;

            var mm = new MultiMesh();
            mm.Mesh = quadMesh;
            mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
            mm.InstanceCount = count;

            for (int i = 0; i < count; i++)
                mm.SetInstanceTransform(i, bucket.Transforms[i]);

            bucket.Instance.Multimesh = mm;
        }
    }
}
