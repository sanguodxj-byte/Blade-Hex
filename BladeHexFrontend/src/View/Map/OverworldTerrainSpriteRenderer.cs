// OverworldTerrainSpriteRenderer.cs
// 大地图手绘地形精灵立牌层 — 羊皮纸地图风格
// 每种地形类型的精灵按 terrain+variant 分桶 MultiMesh 批处理
// 使用 FixedY billboard（竖直面向相机）
// 精灵来源：res://src/assets/tiles/overworld/{overworldKey}_{variant}.png
using Godot;
using System.Collections.Generic;
using BladeHex.Map;

namespace BladeHex.View.Map;

/// <summary>
/// 大地图手绘地形精灵渲染器。
/// 
/// 设计：
/// - 每种 terrain+variant 组合一个 MultiMeshInstance3D 桶
/// - 精灵使用 FixedY billboard（竖直面向相机，底部锚定在 hex 顶面）
/// - 精灵密度由 TerrainSpriteDensity 表控制（不是每个 hex 都有精灵）
/// - 草地/平原/道路等平坦地形不放精灵或极稀疏
/// - 森林/山地等特征地形高密度放置
/// 
/// 性能：数千个精灵只需要 ~50 个 MultiMesh 节点（按 terrain+variant 分桶）
/// </summary>
public partial class OverworldTerrainSpriteRenderer : Node3D
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>像素到 3D 世界坐标的缩放因子</summary>
    private const float PixelToWorld = 1.0f / 156.0f;

    /// <summary>精灵资产基础路径</summary>
    private const string SpriteBaseDir = "res://src/assets/tiles/overworld";

    /// <summary>精灵世界高度（世界单位）— 控制精灵在场景中的视觉大小</summary>
    private const float SpriteWorldHeight = 1.8f;

    /// <summary>精灵底部离 hex 顶面的高度偏移（避免 Z-fighting）</summary>
    private const float BaseElevation = 0.02f;

    /// <summary>相机正交 Size 超过此值时隐藏精灵（LOD）</summary>
    private const float LodHideThreshold = 25.0f;

    /// <summary>相机正交 Size 低于此值时完全显示精灵</summary>
    private const float LodShowThreshold = 20.0f;

    // ========================================
    // 精灵密度配置
    // ========================================

    /// <summary>地形精灵密度（0=不放置，1=每个hex都放）</summary>
    private static readonly Dictionary<HexOverworldTile.TerrainType, float> SpriteDensity = new()
    {
        // 高密度 — 每个 hex 都有精灵
        [HexOverworldTile.TerrainType.Forest] = 1.0f,
        [HexOverworldTile.TerrainType.DenseForest] = 1.0f,
        [HexOverworldTile.TerrainType.Jungle] = 1.0f,
        [HexOverworldTile.TerrainType.Mountain] = 1.0f,
        [HexOverworldTile.TerrainType.MountainSnow] = 1.0f,
        [HexOverworldTile.TerrainType.Taiga] = 1.0f,

        // 中密度
        [HexOverworldTile.TerrainType.Hills] = 0.7f,
        [HexOverworldTile.TerrainType.Rocky] = 0.6f,
        [HexOverworldTile.TerrainType.Swamp] = 0.7f,
        [HexOverworldTile.TerrainType.Bog] = 0.6f,
        [HexOverworldTile.TerrainType.Sand] = 0.5f,
        [HexOverworldTile.TerrainType.Savanna] = 0.5f,
        [HexOverworldTile.TerrainType.DeepWater] = 0.6f,
        [HexOverworldTile.TerrainType.ShallowWater] = 0.6f,
        [HexOverworldTile.TerrainType.River] = 0.5f,
        [HexOverworldTile.TerrainType.Wasteland] = 0.4f,
        [HexOverworldTile.TerrainType.Snow] = 0.3f,
        [HexOverworldTile.TerrainType.Ice] = 0.3f,

        // 低密度 — 大部分 hex 不放精灵
        [HexOverworldTile.TerrainType.Grassland] = 0.15f,
        [HexOverworldTile.TerrainType.Plains] = 0.10f,

        // 无精灵
        [HexOverworldTile.TerrainType.Road] = 0.0f,
    };

    // ========================================
    // 桶结构
    // ========================================

    private class SpriteBucket
    {
        public string SpriteKey = "";
        public MultiMeshInstance3D Instance = null!;
        public List<Transform3D> Transforms = new();
        public bool Dirty;
        public float AspectRatio = 1.0f; // width / height
    }

    // ========================================
    // 状态
    // ========================================

    private readonly Dictionary<string, SpriteBucket> _buckets = new();
    private readonly HashSet<Vector2I> _loadedTiles = new();
    private readonly Dictionary<string, Texture2D> _textureCache = new();
    private bool _visible = true;

    // ========================================
    // 初始化
    // ========================================

    /// <summary>初始化渲染器</summary>
    public void Initialize()
    {
        Name = "OverworldTerrainSpriteRenderer";
    }

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 为一批 tile 生成并加载地形精灵到 MultiMesh 桶。
    /// </summary>
    public void LoadSpritesForTiles(IEnumerable<HexOverworldTile> tiles)
    {
        bool anyNew = false;

        foreach (var tile in tiles)
        {
            if (_loadedTiles.Contains(tile.Coord)) continue;
            _loadedTiles.Add(tile.Coord);

            // 检查密度 — 是否为此 tile 放置精灵
            if (!ShouldPlaceSprite(tile))
                continue;

            // 确定精灵 key（terrain + variant）
            var profile = TerrainVisualRegistry.Get(tile.Terrain);
            int variantCount = Mathf.Max(profile.OverworldVariantCount, 1);
            int variant = HashTileVariant(tile.Coord, variantCount);
            string spriteKey = $"{profile.OverworldKey}_{variant}";

            // 加载纹理（缓存）
            var texture = GetOrLoadTexture(spriteKey);
            if (texture == null) continue;

            // 计算世界位置
            float elevY = GetTileElevationY(tile);
            float worldX = tile.PixelPos.X * PixelToWorld;
            float worldZ = tile.PixelPos.Y * PixelToWorld;

            // 精灵底部锚定在 hex 顶面
            float aspectRatio = (float)texture.GetWidth() / texture.GetHeight();
            float spriteHeight = SpriteWorldHeight;
            float yPos = elevY + BaseElevation + spriteHeight * 0.5f;

            // 微小随机偏移（确定性）避免精灵完全对齐网格
            float offsetX = (HashTileVariant(tile.Coord + new Vector2I(7, 13), 60) - 30) * 0.005f;
            float offsetZ = (HashTileVariant(tile.Coord + new Vector2I(13, 7), 60) - 30) * 0.005f;

            var transform = new Transform3D(
                Basis.Identity,
                new Vector3(worldX + offsetX, yPos, worldZ + offsetZ));

            AddToSpriteBucket(spriteKey, texture, transform, aspectRatio);
            anyNew = true;
        }

        if (anyNew)
            RebuildDirtyBuckets();
    }

    /// <summary>清除所有精灵</summary>
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

    /// <summary>
    /// 每帧调用，根据相机正交 Size 控制精灵层可见性（LOD）。
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

    /// <summary>当前已加载的精灵总数</summary>
    public int SpriteCount
    {
        get
        {
            int count = 0;
            foreach (var kvp in _buckets) count += kvp.Value.Transforms.Count;
            return count;
        }
    }

    // ========================================
    // 内部：密度判定
    // ========================================

    /// <summary>根据地形密度配置决定是否为此 tile 放置精灵</summary>
    private static bool ShouldPlaceSprite(HexOverworldTile tile)
    {
        if (!SpriteDensity.TryGetValue(tile.Terrain, out float density))
            return false;

        if (density >= 1.0f) return true;
        if (density <= 0.0f) return false;

        // 确定性伪随机判定
        float roll = HashTileVariant(tile.Coord, 1000) / 1000.0f;
        return roll < density;
    }

    // ========================================
    // 内部：桶管理
    // ========================================

    private void AddToSpriteBucket(string spriteKey, Texture2D texture, Transform3D transform, float aspectRatio)
    {
        if (!_buckets.TryGetValue(spriteKey, out var bucket))
        {
            bucket = CreateSpriteBucket(spriteKey, texture, aspectRatio);
            _buckets[spriteKey] = bucket;
        }

        bucket.Transforms.Add(transform);
        bucket.Dirty = true;
    }

    private SpriteBucket CreateSpriteBucket(string spriteKey, Texture2D texture, float aspectRatio)
    {
        // 材质：StandardMaterial3D + FixedY billboard + alpha scissor
        var mat = new StandardMaterial3D();
        mat.AlbedoTexture = texture;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY;
        mat.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
        mat.AlphaScissorThreshold = 0.5f;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

        var instance = new MultiMeshInstance3D();
        instance.Name = $"TerrainSprite_{spriteKey}";
        instance.MaterialOverride = mat;
        instance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(instance);

        return new SpriteBucket
        {
            SpriteKey = spriteKey,
            Instance = instance,
            AspectRatio = aspectRatio,
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

            // QuadMesh 尺寸：高度固定，宽度按纹理比例
            float meshH = SpriteWorldHeight;
            float meshW = meshH * bucket.AspectRatio;

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

    // ========================================
    // 内部：纹理加载
    // ========================================

    /// <summary>加载精灵纹理（带缓存）</summary>
    private Texture2D? GetOrLoadTexture(string spriteKey)
    {
        if (_textureCache.TryGetValue(spriteKey, out var cached))
            return cached;

        string path = $"{SpriteBaseDir}/{spriteKey}.png";
        if (!ResourceLoader.Exists(path))
        {
            // 缓存 null 避免重复查找
            return null;
        }

        var tex = GD.Load<Texture2D>(path);
        if (tex != null)
            _textureCache[spriteKey] = tex;
        return tex;
    }

    // ========================================
    // 内部：工具方法
    // ========================================

    /// <summary>根据 tile 坐标生成稳定的伪随机变体索引</summary>
    private static int HashTileVariant(Vector2I coord, int range)
    {
        int hash = coord.X * 73856093 ^ coord.Y * 19349663;
        hash = ((hash >> 16) ^ hash) * 0x45d9f3b;
        hash = ((hash >> 16) ^ hash);
        return ((hash % range) + range) % range;
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
}
