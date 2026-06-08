// GrassOverlayBatcher.cs
// 战斗场景地表精灵叠加层 —— ★这是玩家实际看到的地表★
//
// 重要架构说明：
//   六棱柱顶面(HexCellMultiMeshBatcher + battle_ground_top.gdshader)在多数地形下
//   只是结构/占位，其顶面视觉几乎完全被本层 2D 精灵覆盖(PlacementChance=1.0 满覆盖)。
//   因此地表的受光/着色以本层为准，改 battle_ground_top shader 对可见地表通常无效。
//
// 在 Grassland / Plains / Savanna / Forest 等地形的 hex 顶面放置地表精灵。
// 精灵故意超出 hex 边界 ~20%，通过 Y-sort 实现自然层叠。
// 使用 Sprite3D 平铺在地面（非 Billboard），配合 alpha 羽化边缘；Shaded=true 接收真实方向光。
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.View.AssetSystem;

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

    /// <summary>高差边草地悬挑出崖唇的世界单位余量：补偿精灵悬空高度在 -45° 俯角下的视差，
    /// 避免硬裁后崖唇露出下方棱柱顶面。仅作用于高差边（见 PlaceOverlaySprite）。
    /// 精灵悬空高 = YOffset(0.5) + yJitter(0–2)，最大 ~2.5；-45° 下视差位移≈悬空高，
    /// 故余量取略大于最大视差即可。实测扫 3/5/8：3 刚好盖缝且外扩最小，8 已明显鼓出崖唇。</summary>
    private const float CliffOverhang = 3.0f;

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
        BattleCellData.TerrainType.Jungle,
        BattleCellData.TerrainType.Taiga,
    };

    /// <summary>适合放置荒地纹理的地形类型</summary>
    private static readonly HashSet<BattleCellData.TerrainType> DirtTerrains = new()
    {
        BattleCellData.TerrainType.Sand,
        BattleCellData.TerrainType.Wasteland,
    };

    /// <summary>适合放置泥土纹理的地形类型</summary>
    private static readonly HashSet<BattleCellData.TerrainType> MudTerrains = new()
    {
        BattleCellData.TerrainType.Hills,
        BattleCellData.TerrainType.Swamp,
        BattleCellData.TerrainType.Bog,
    };

    /// <summary>适合放置石板路纹理的地形类型</summary>
    private static readonly HashSet<BattleCellData.TerrainType> RoadTerrains = new()
    {
        BattleCellData.TerrainType.Road,
        BattleCellData.TerrainType.Bridge,
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
        BattleCellData.TerrainType.Rocky,
    };

    /// <summary>适合放置毒菇毒孢子地表的地形类型</summary>
    private static readonly HashSet<BattleCellData.TerrainType> PoisonMushroomTerrains = new()
    {
        BattleCellData.TerrainType.PoisonMushroom,
    };


    // ========================================
    // 状态
    // ========================================

    private readonly List<Sprite3D> _sprites = new();

    /// <summary>高差硬裁 shader（仅触及高差的精灵才套用，只加载一次）</summary>
    private const string OverlayShaderPath = "res://BladeHexFrontend/src/assets/shaders/battle_ground_overlay.gdshader";
    private static Shader? _overlayShader;

    private Texture2D[]? _grassTextures;
    private Texture2D[]? _wastelandTextures;
    private Texture2D[]? _mudTextures;
    private Texture2D[]? _roadTextures;
    private Texture2D[]? _snowTextures;
    private Texture2D[]? _rockTextures;
    private Texture2D[]? _poisonMushroomTextures;

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
        LoadPoisonMushroomTextures();

        int placed = 0;
        foreach (var kvp in hexGrid.Cells)
        {
            var cell = kvp.Value;
            if (cell == null || !GodotObject.IsInstanceValid(cell)) continue;

            var terrain = cell.Data?.terrainType ?? BattleCellData.TerrainType.Plains;

            // 废墟地形特殊处理：直接使用临近地形的遮盖层
            if (terrain == BattleCellData.TerrainType.Ruins)
            {
                var neighbors = HexUtils.GetNeighbors(cell.GridPos.X, cell.GridPos.Y);
                bool found = false;
                foreach (var nbPos in neighbors)
                {
                    if (hexGrid.Cells.TryGetValue(nbPos, out var nbCell) && nbCell?.Data != null)
                    {
                        var nbTerrain = nbCell.Data.terrainType;
                        if (nbTerrain != BattleCellData.TerrainType.Ruins && 
                            nbTerrain != BattleCellData.TerrainType.Wall &&
                            nbTerrain != BattleCellData.TerrainType.DeepWater &&
                            nbTerrain != BattleCellData.TerrainType.ShallowWater &&
                            nbTerrain != BattleCellData.TerrainType.River)
                        {
                            terrain = nbTerrain;
                            found = true;
                            break;
                        }
                    }
                }
                if (!found)
                {
                    terrain = BattleCellData.TerrainType.Plains;
                }
            }

            if (GrassTerrains.Contains(terrain) && _grassTextures != null && _grassTextures.Length > 0)
            {
                PlaceOverlaySprite(hexGrid, cell, _grassTextures);
                placed++;
            }
            else if (DirtTerrains.Contains(terrain) && _wastelandTextures != null && _wastelandTextures.Length > 0)
            {
                PlaceOverlaySprite(hexGrid, cell, _wastelandTextures);
                placed++;
            }
            else if (MudTerrains.Contains(terrain) && _mudTextures != null && _mudTextures.Length > 0)
            {
                PlaceOverlaySprite(hexGrid, cell, _mudTextures);
                placed++;
            }
            else if (RoadTerrains.Contains(terrain) && _roadTextures != null && _roadTextures.Length > 0)
            {
                PlaceOverlaySprite(hexGrid, cell, _roadTextures);
                placed++;
            }
            else if (SnowTerrains.Contains(terrain) && _snowTextures != null && _snowTextures.Length > 0)
            {
                PlaceOverlaySprite(hexGrid, cell, _snowTextures);
                placed++;
            }
            else if (RockTerrains.Contains(terrain) && _rockTextures != null && _rockTextures.Length > 0)
            {
                PlaceOverlaySprite(hexGrid, cell, _rockTextures);
                placed++;
            }
            else if (PoisonMushroomTerrains.Contains(terrain) && _poisonMushroomTextures != null && _poisonMushroomTextures.Length > 0)
            {
                PlaceOverlaySprite(hexGrid, cell, _poisonMushroomTextures);
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

    private void LoadPoisonMushroomTextures()
    {
        if (_poisonMushroomTextures != null) return;
        _poisonMushroomTextures = LoadTextureSet("res://assets/sprites/poison_mushroom_patches/", "poison_mushroom", 16);
        GD.Print($"[GrassOverlayBatcher] 加载了 {_poisonMushroomTextures.Length} 个毒菇纹理");
    }


    private static Texture2D[] LoadTextureSet(string basePath, string prefix, int count)
    {
        var textures = new List<Texture2D>();
        for (int i = 0; i < count; i++)
        {
            string path = $"{basePath}{prefix}_{i:D2}.png";
            string assetId = $"{prefix}_{i:D2}";
            var texture = TextureAssetResolver.LoadMapTexture(assetId, path);
            if (texture != null)
                textures.Add(texture);
        }
        return textures.ToArray();
    }

    private void PlaceOverlaySprite(HexGrid hexGrid, HexCell cell, Texture2D[] textures)
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

        // 接收真实光照：平铺精灵法线朝上(随 -90° 旋转)，Shaded 后被 Godot 方向光按上向 ndl
        // 照亮，地表亮度随太阳角度/能量/色温变化，与场景统一光照。
        sprite.Shaded = true;

        // OpaquePrepass：实心核心(alpha≥阈值)写深度 → 能接收树/石 prop 投来的真实阴影；
        // 羽化边缘(alpha<阈值)仍只做颜色混合、不写深度，保留无缝拼接并避免重叠地块 z-fighting。
        // 各地块实心核心约为 hex 大小、基本边对边平铺、核心重叠很小，故核心间 z-fighting 可忽略。
        sprite.AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass;
        sprite.AlphaScissorThreshold = 0.5f;
        sprite.Transparent = true;
        sprite.NoDepthTest = false;
        sprite.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
        // 渲染优先级：草地在地面之上、单位之下
        sprite.RenderPriority = -1;

        // 禁用阴影投射（草地精灵不需要投影）
        sprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        // 高度色调：按 cell.Elevation 调制精灵颜色，让同地形的不同高度在视觉上可区分。
        // 战斗地图 elevation 实际范围 0–5，基准（平地）= 2（见 BattleMapGenerator.FinalizeCells）。
        Color tint = ElevationTint(cell.Elevation);

        // 高差硬裁：仅当本格与某邻格高差 ≥1 级时，套高差裁切 shader，把溢出到低/高邻格上空的
        // 羽化边缘沿共享边一刀切平（消除纹理浮空）；纯内部平坦格走 Sprite3D 内建路径、零额外开销。
        // 注意：套 MaterialOverride 后 Sprite3D.Modulate 失效，色调改走 shader 的 modulate uniform。
        int cliffMask = ComputeCliffMask(hexGrid, cell);
        if (cliffMask != 0)
        {
            _overlayShader ??= ShaderAssetResolver.Load("battle_ground_overlay", OverlayShaderPath);
            if (_overlayShader != null)
            {
                // apothem 加一点悬挑余量(CliffOverhang)：精灵悬空在棱柱顶面上方
                // (YOffset+yJitter ~0.5–2.5)，战斗相机 -45° 俯角下裁切边正落在崖唇会因视差
                // 缩进、露出下方棱柱顶面一条缝。让高差边的草地略微悬挑出崖唇盖住该缝
                // (悬挑部分正好挂在崖面上方，不影响同高邻格接缝)。实测 12 单位最佳平衡。
                float apothem = HexUtils.Size * Mathf.Sqrt(3.0f) / 2.0f + CliffOverhang;
                float halfExtent = targetWorldSize * 0.5f;
                var mat = new ShaderMaterial { Shader = _overlayShader };
                mat.SetShaderParameter("tex_albedo", texture);
                mat.SetShaderParameter("cliff_mask", cliffMask);
                mat.SetShaderParameter("apothem", apothem);
                mat.SetShaderParameter("half_extent", halfExtent);
                mat.SetShaderParameter("modulate", tint);
                sprite.MaterialOverride = mat;
            }
            else
            {
                sprite.Modulate = tint;
            }
        }
        else
        {
            sprite.Modulate = tint;
        }

        // 位置：精确居中在 hex 顶面（不偏移，靠 OverflowScale 重叠覆盖缝隙）
        // 每格按格坐标加一个确定性的微小 Y 错位(0~2 单位)：OpaquePrepass 下重叠地块共面会
        // 深度打架，移动视角时 hex 边缘闪烁；错开高度使其深度唯一、消除闪烁。
        // 2 单位相对 hex(96)且 45° 俯视下肉眼不可见，仍能接收 prop 投影。
        int gridHash = (cell.GridPos.X * 73856093) ^ (cell.GridPos.Y * 19349663);
        float yJitter = (gridHash & 0xFF) / 255.0f * 2.0f;
        float hexHeight = HexUtils.Size * 0.5f;
        sprite.Position = cell.Position + new Vector3(0, hexHeight / 2.0f + YOffset + yJitter, 0);

        // 固定朝上
        sprite.RotationDegrees = new Vector3(-90, 0, 0);

        // Y-sort: 使用 Z 坐标作为排序依据（Godot 3D 中 Y-sort 通过渲染优先级实现）
        // 在正交相机下，Z 值越大越靠近相机（后渲染）
        sprite.SortingOffset = cell.Position.Z * 0.001f;

        AddChild(sprite);
        _sprites.Add(sprite);
    }

    /// <summary>
    /// 计算高差裁切掩码：6 位，bit d = 1 表示 dir d 的邻格与本格高差 ≥1 级，需沿该边硬裁。
    /// 地图边界（无邻格）的边不设位，保留贴图羽化自然淡出。
    /// 位序与 HexUtils.Directions 一致，shader 内按相同 d 取边外法线（30°+60°·d）。
    /// </summary>
    private static int ComputeCliffMask(HexGrid hexGrid, HexCell cell)
    {
        int mask = 0;
        for (int d = 0; d < 6; d++)
        {
            var nb = HexUtils.GetNeighbor(cell.GridPos.X, cell.GridPos.Y, d);
            if (hexGrid.Cells.TryGetValue(nb, out var nbCell) && nbCell != null
                && Mathf.Abs(cell.Elevation - nbCell.Elevation) >= 1)
            {
                mask |= (1 << d);
            }
        }
        return mask;
    }

    /// <summary>
    /// 高度色调阶梯：以 elevation=2（平地基准）为中心，返回用于 Sprite3D.Modulate 的颜色乘子。
    /// Modulate 是逐通道乘子，故只做两件 modulate 能真实表达的事：
    ///   1) 明度——高于基准提亮、低于基准压暗；
    ///   2) 冷暖偏移——高地略偏冷（蓝），低地略偏暖（红），模拟空气透视的远近感。
    /// 战斗地图 elevation 实际范围 0–5，基准 = 2（见 BattleMapGenerator.FinalizeCells）。
    /// </summary>
    private const int BaselineElevation = 2;

    private static Color ElevationTint(int elevation)
    {
        int delta = Mathf.Clamp(elevation - BaselineElevation, -2, 3);

        float brightness = 1.0f + delta * 0.10f;   // 每级 ±10% 明度
        float warmCool = delta * 0.015f;           // 每级冷暖偏移：高地偏冷、低地偏暖

        float r = Mathf.Clamp(brightness - warmCool, 0f, 2f);
        float g = Mathf.Clamp(brightness, 0f, 2f);
        float b = Mathf.Clamp(brightness + warmCool, 0f, 2f);

        return new Color(r, g, b, 1.0f);
    }
}
