// CombatMaterialManager.cs
// 战斗场景 3D 材质管理器 — 缓存和生成带有地形纹理的材质
// 迁移自 GDScript CombatMaterialManager.gd
using Godot;
using System.Collections.Generic;
using BladeHex.Data;

namespace BladeHex.Map;

/// <summary>
/// 战斗场景材质管理器 — 单例
/// </summary>
[GlobalClass]
public partial class CombatMaterialManager : RefCounted
{
    private static CombatMaterialManager? _instance;
    private readonly Dictionary<string, StandardMaterial3D> _materials = new();

    public static CombatMaterialManager GetInstance()
    {
        _instance ??= new CombatMaterialManager();
        return _instance;
    }

    /// <summary>根据地形类型和高程获取对应的 3D 材质</summary>
    public StandardMaterial3D GetMaterial(BattleCellData.TerrainType terrainType, int elevation)
    {
        string key = $"{(int)terrainType}_{elevation}";
        if (_materials.TryGetValue(key, out StandardMaterial3D? existing))
            return existing;

        var mat = new StandardMaterial3D();
        string texName = GetTextureName(terrainType);

        // 尝试加载带高度差的专属贴图
        string texPath = $"res://src/assets/tiles/hex_terrain/{texName}_elev{elevation}_0.png";
        var tex = GD.Load<Texture2D>(texPath);

        if (tex == null)
        {
            texPath = $"res://src/assets/tiles/hex_terrain/{texName}_0.png";
            tex = GD.Load<Texture2D>(texPath);
        }

        bool hasTexture = false;
        if (tex != null)
        {
            mat.AlbedoTexture = tex;
            mat.TextureFilter = StandardMaterial3D.TextureFilterEnum.Nearest;
            mat.Uv1Triplanar = true;
            mat.Uv1Scale = new Vector3(0.01f, 0.01f, 0.01f);
            hasTexture = true;
        }

        // 混合颜色
        Color baseColor;
        if (hasTexture)
            baseColor = Colors.White;
        else
        {
            var props = BattleCellData.GetTerrainProperties(terrainType);
            baseColor = props.Color;
        }

        // 根据高程进行轻微的明暗调整
        if (elevation == 0)
            baseColor = baseColor.Darkened(0.2f);
        else if (elevation == 2)
            baseColor = baseColor.Lightened(0.2f);

        mat.AlbedoColor = baseColor;
        _materials[key] = mat;
        return mat;
    }

    private static string GetTextureName(BattleCellData.TerrainType type) => type switch
    {
        BattleCellData.TerrainType.Plains => "grassland",
        BattleCellData.TerrainType.Grassland => "grassland",
        BattleCellData.TerrainType.Savanna => "barren_land",
        BattleCellData.TerrainType.Forest => "forest",
        BattleCellData.TerrainType.DenseForest => "forest",
        BattleCellData.TerrainType.Hills => "rocky_land",
        BattleCellData.TerrainType.Mountain => "mountain_cave",
        BattleCellData.TerrainType.ShallowWater => "pond",
        BattleCellData.TerrainType.DeepWater => "pond",
        BattleCellData.TerrainType.Swamp => "swamp",
        BattleCellData.TerrainType.Road => "crossroads",
        BattleCellData.TerrainType.Sand => "wasteland",
        BattleCellData.TerrainType.Snow => "mountain_cave",
        BattleCellData.TerrainType.Wall => "castle",
        BattleCellData.TerrainType.Ruins => "ruins",
        BattleCellData.TerrainType.PoisonMushroom => "swamp",
        BattleCellData.TerrainType.LuckyGrass => "grassland",
        _ => "grassland",
    };
}
