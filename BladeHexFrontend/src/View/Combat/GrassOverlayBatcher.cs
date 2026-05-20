// GrassOverlayBatcher.cs
// 战斗场景草地精灵叠加层
// 在 Grassland / Plains / Savanna / Forest 等地形的 hex 顶面放置草地精灵。
// 精灵故意超出 hex 边界 ~20%，通过 Y-sort 实现自然层叠。
// 使用 Sprite3D 平铺在地面（非 Billboard），配合 alpha 羽化边缘。
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.View.Combat;

/// <summary>
/// 草地精灵叠加层 — 在适合的地形 hex 上放置草地纹理精灵。
/// 精灵平铺在 hex 顶面，略大于 hex（溢出 20%），Y-sort 排序。
/// </summary>
[GlobalClass]
public partial class GrassOverlayBatcher : Node3D
{
    // ========================================
    // 配置
    // ========================================

    /// <summary>草地精灵溢出 hex 边界的比例（1.2 = 超出 20%）</summary>
    private const float OverflowScale = 1.2f;

    /// <summary>每个 hex 放置草地精灵的概率（1.0 = 全覆盖无空隙）</summary>
    private const float PlacementChance = 1.0f;

    /// <summary>精灵 Y 轴偏移（纹理层）</summary>
    private static readonly float YOffset = CombatLayerHeight.TextureLayer;

    /// <summary>适合放置草地的地形类型</summary>
    private static readonly HashSet<BattleCellData.TerrainType> GrassTerrains = new()
    {
        BattleCellData.TerrainType.Grassland,
        BattleCellData.TerrainType.Plains,
        BattleCellData.TerrainType.Savanna,
        BattleCellData.TerrainType.Forest,
        BattleCellData.TerrainType.DenseForest,
    };

    /// <summary>适合放置荒地纹理的地形类型</summary>
    private static readonly HashSet<BattleCellData.TerrainType> DirtTerrains = new()
    {
        BattleCellData.TerrainType.Sand,
    };

    /// <summary>适合放置泥土纹理的地形类型</summary>
    private static readonly HashSet<BattleCellData.TerrainType> MudTerrains = new()
    {
        BattleCellData.TerrainType.Hills,
        BattleCellData.TerrainType.Swamp,
    };

    /// <summary>适合放置石板路纹理的地形类型</summary>
    private static readonly HashSet<BattleCellData.TerrainType> RoadTerrains = new()
    {
        BattleCellData.TerrainType.Road,
    };

    /// <summary>适合放置雪地纹理的地形类型</summary>
    private static readonly HashSet<BattleCellData.TerrainType> SnowTerrains = new()
    {
        BattleCellData.TerrainType.Snow,
        BattleCellData.TerrainType.Ice,
        BattleCellData.TerrainType.MountainSnow,
    };

    /// <summary>适合放置岩石纹理的地形类型</summary>
    private static readonly HashSet<BattleCellData.TerrainType> RockTerrains = new()
    {
        BattleCellData.TerrainType.Mountain,
    };

    // ========================================
    // 状态
    // ========================================

    private readonly List<Sprite3D> _sprites = new();
    private Texture2D[]? _grassTextures;
    private Texture2D[]? _wastelandTextures;
    private Texture2D[]? _mudTextures;
    private Texture2D[]? _roadTextures;
    private Texture2D[]? _snowTextures;
    private Texture2D[]? _rockTextures;

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 在适合的 hex 上放置草地精灵叠加层。
    /// 在 PlaceDecorations 之前调用（草地在装饰物下方）。
    /// </summary>
    public void PlaceGrassOverlays(HexGrid hexGrid, BattleMapGenerator.BattleMapData? mapData)
    {
        if (hexGrid == null) return;

        // 加载纹理
        LoadGrassTextures();
        LoadWastelandTextures();
        LoadMudTextures();
        LoadRoadTextures();
        LoadSnowTextures();
        LoadRockTextures();

        int placed = 0;
        foreach (var kvp in hexGrid.Cells)
        {
            var cell = kvp.Value;
            if (cell == null || !GodotObject.IsInstanceValid(cell)) continue;

            var terrain = cell.Data?.terrainType ?? BattleCellData.TerrainType.Plains;

            if (GrassTerrains.Contains(terrain) && _grassTextures != null && _grassTextures.Length > 0)
            {
                PlaceOverlaySprite(cell, _grassTextures);
                placed++;
            }
            else if (DirtTerrains.Contains(terrain) && _wastelandTextures != null && _wastelandTextures.Length > 0)
            {
                PlaceOverlaySprite(cell, _wastelandTextures);
                placed++;
            }
            else if (MudTerrains.Contains(terrain) && _mudTextures != null && _mudTextures.Length > 0)
            {
                PlaceOverlaySprite(cell, _mudTextures);
                placed++;
            }
            else if (RoadTerrains.Contains(terrain) && _roadTextures != null && _roadTextures.Length > 0)
            {
                PlaceOverlaySprite(cell, _roadTextures);
                placed++;
            }
            else if (SnowTerrains.Contains(terrain) && _snowTextures != null && _snowTextures.Length > 0)
            {
                PlaceOverlaySprite(cell, _snowTextures);
                placed++;
            }
            else if (RockTerrains.Contains(terrain) && _rockTextures != null && _rockTextures.Length > 0)
            {
                PlaceOverlaySprite(cell, _rockTextures);
                placed++;
            }
        }

        GD.Print($"[GrassOverlayBatcher] 放置了 {placed} 个地形覆盖精灵");
    }

    /// <summary>清除所有草地精灵</summary>
    public void ClearOverlays()
    {
        foreach (var sprite in _sprites)
        {
            if (GodotObject.IsInstanceValid(sprite))
                sprite.QueueFree();
        }
        _sprites.Clear();
    }

    // ========================================
    // 内部方法
    // ========================================

    private void LoadGrassTextures()
    {
        if (_grassTextures != null) return;
        _grassTextures = LoadTextureSet("res://assets/sprites/grass_patches/", "grass", 16);
        GD.Print($"[GrassOverlayBatcher] 加载了 {_grassTextures.Length} 个草地纹理");
    }

    private void LoadWastelandTextures()
    {
        if (_wastelandTextures != null) return;
        _wastelandTextures = LoadTextureSet("res://assets/sprites/wasteland_patches/", "wasteland", 16);
        GD.Print($"[GrassOverlayBatcher] 加载了 {_wastelandTextures.Length} 个荒地纹理");
    }

    private void LoadMudTextures()
    {
        if (_mudTextures != null) return;
        _mudTextures = LoadTextureSet("res://assets/sprites/dirt_patches/", "dirt", 16);
        GD.Print($"[GrassOverlayBatcher] 加载了 {_mudTextures.Length} 个泥土纹理");
    }

    private void LoadRoadTextures()
    {
        if (_roadTextures != null) return;
        _roadTextures = LoadTextureSet("res://assets/sprites/road_patches/", "road", 16);
        GD.Print($"[GrassOverlayBatcher] 加载了 {_roadTextures.Length} 个石板路纹理");
    }

    private void LoadSnowTextures()
    {
        if (_snowTextures != null) return;
        _snowTextures = LoadTextureSet("res://assets/sprites/snow_patches/", "snow", 16);
        GD.Print($"[GrassOverlayBatcher] 加载了 {_snowTextures.Length} 个雪地纹理");
    }

    private void LoadRockTextures()
    {
        if (_rockTextures != null) return;
        _rockTextures = LoadTextureSet("res://assets/sprites/rock_patches/", "rock", 16);
        GD.Print($"[GrassOverlayBatcher] 加载了 {_rockTextures.Length} 个岩石纹理");
    }

    private static Texture2D[] LoadTextureSet(string basePath, string prefix, int count)
    {
        var textures = new List<Texture2D>();
        for (int i = 0; i < count; i++)
        {
            string path = $"{basePath}{prefix}_{i:D2}.png";
            if (ResourceLoader.Exists(path))
            {
                var tex = ResourceLoader.Load<Texture2D>(path);
                if (tex != null) textures.Add(tex);
            }
        }
        if (textures.Count == 0)
        {
            for (int i = 0; i < count; i++)
            {
                string path = $"{basePath}{prefix}_{i:D2}.png";
                if (FileAccess.FileExists(path))
                {
                    var image = Image.LoadFromFile(path);
                    if (image != null)
                        textures.Add(ImageTexture.CreateFromImage(image));
                }
            }
        }
        return textures.ToArray();
    }

    private void PlaceOverlaySprite(HexCell cell, Texture2D[] textures)
    {
        if (textures.Length == 0) return;

        // 随机选择纹理
        int texIdx = (int)(GD.Randf() * textures.Length);
        var texture = textures[texIdx];

        // 创建 Sprite3D — 平铺在地面（不使用 Billboard）
        var sprite = new Sprite3D();
        sprite.Texture = texture;

        // 计算 PixelSize 使精灵覆盖 hex 直径 × OverflowScale
        // hex 直径 = 2 × HexUtils.Size = 192 世界单位
        // Sprite3D 的世界宽度 = texture.Width × PixelSize
        // 目标世界宽度 = hex 直径 × OverflowScale
        float targetWorldSize = HexUtils.Size * 2.0f * OverflowScale;
        float texWidth = texture.GetWidth();
        float pixelSize = targetWorldSize / texWidth;
        sprite.PixelSize = pixelSize;

        // 平铺在地面：旋转 -90° 使精灵面朝上（XZ 平面）
        sprite.RotationDegrees = new Vector3(-90, 0, 0);

        // 不使用 Billboard — 精灵贴在地面
        sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;

        // Alpha 处理：禁用硬切，使用完全混合让边缘羽化自然过渡
        sprite.AlphaCut = SpriteBase3D.AlphaCutMode.Disabled;
        sprite.Transparent = true;
        sprite.NoDepthTest = false;
        sprite.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
        // 渲染优先级：草地在地面之上、单位之下
        sprite.RenderPriority = -1;

        // 禁用阴影投射（草地精灵不需要投影）
        sprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        // 位置：精确居中在 hex 顶面（不偏移，靠 OverflowScale 重叠覆盖缝隙）
        float hexHeight = HexUtils.Size * 0.5f;
        sprite.Position = cell.Position + new Vector3(0, hexHeight / 2.0f + YOffset, 0);

        // 固定朝上
        sprite.RotationDegrees = new Vector3(-90, 0, 0);

        // Y-sort: 使用 Z 坐标作为排序依据（Godot 3D 中 Y-sort 通过渲染优先级实现）
        // 在正交相机下，Z 值越大越靠近相机（后渲染）
        sprite.SortingOffset = cell.Position.Z * 0.001f;

        AddChild(sprite);
        _sprites.Add(sprite);
    }
}
