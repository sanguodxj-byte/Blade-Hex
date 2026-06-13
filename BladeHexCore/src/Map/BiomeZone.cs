// BiomeZone.cs
// 生态区数据模型 — 地形聚类后的连通区域
// 一个生态区是"同类地形的一块连通土地"，由 flood-fill 识别
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Map;

/// <summary>
/// 生态区 — 由 BiomeZoneAnalyzer 通过 flood-fill 识别的连通区域
/// 代表一块具有相同真实地形类型的连续土地
/// </summary>
public class BiomeZone
{
    /// <summary>聚类 ID（生成时分配的唯一序号）</summary>
    public int Id { get; set; }

    /// <summary>主导生态类型</summary>
    public BiomeType DominantBiome { get; set; }

    /// <summary>Exact terrain type used to split and name this zone.</summary>
    public HexOverworldTile.TerrainType DominantTerrain { get; set; } = HexOverworldTile.TerrainType.Plains;

    /// <summary>属于这个生态区的所有 tile 全局轴向坐标</summary>
    public HashSet<Vector2I> TileCoords { get; set; } = new();

    /// <summary>tile 数量</summary>
    public int TileCount => TileCoords.Count;

    /// <summary>几何中心（轴向坐标）</summary>
    public Vector2I Centroid { get; set; }

    /// <summary>平均高程</summary>
    public float AverageElevation { get; set; }

    /// <summary>平均温度</summary>
    public float AverageTemperature { get; set; }

    /// <summary>平均湿度</summary>
    public float AverageMoisture { get; set; }

    /// <summary>所属国家 ID（分配后填入，未分配时为空）</summary>
    public string OwnerNationId { get; set; } = "";

    /// <summary>是否已被分配给某个国家</summary>
    public bool IsAssigned => !string.IsNullOrEmpty(OwnerNationId);

    /// <summary>计算几何中心</summary>
    public void ComputeCentroid()
    {
        if (TileCoords.Count == 0) return;
        long sumQ = 0, sumR = 0;
        foreach (var coord in TileCoords)
        {
            sumQ += coord.X;
            sumR += coord.Y;
        }
        Centroid = new Vector2I((int)(sumQ / TileCoords.Count), (int)(sumR / TileCoords.Count));
    }

    /// <summary>序列化为 Godot Dictionary</summary>
    public Godot.Collections.Dictionary Serialize()
    {
        return new Godot.Collections.Dictionary
        {
            ["id"] = Id,
            ["biome"] = (int)DominantBiome,
            ["terrain"] = (int)DominantTerrain,
            ["tile_count"] = TileCount,
            ["centroid_q"] = Centroid.X,
            ["centroid_r"] = Centroid.Y,
            ["avg_elev"] = AverageElevation,
            ["avg_temp"] = AverageTemperature,
            ["avg_moist"] = AverageMoisture,
            ["owner"] = OwnerNationId,
        };
    }

    /// <summary>反序列化（不含 TileCoords，需要从 chunk 数据重建）</summary>
    public static BiomeZone DeserializeMeta(Godot.Collections.Dictionary data)
    {
        return new BiomeZone
        {
            Id = data.ContainsKey("id") ? (int)data["id"] : 0,
            DominantBiome = data.ContainsKey("biome") ? (BiomeType)(int)data["biome"] : BiomeType.Plains,
            DominantTerrain = data.ContainsKey("terrain")
                ? (HexOverworldTile.TerrainType)(int)data["terrain"]
                : DefaultTerrainForBiome(data.ContainsKey("biome") ? (BiomeType)(int)data["biome"] : BiomeType.Plains),
            Centroid = new Vector2I(
                data.ContainsKey("centroid_q") ? (int)data["centroid_q"] : 0,
                data.ContainsKey("centroid_r") ? (int)data["centroid_r"] : 0),
            AverageElevation = data.ContainsKey("avg_elev") ? (float)data["avg_elev"] : 0.5f,
            AverageTemperature = data.ContainsKey("avg_temp") ? (float)data["avg_temp"] : 0.5f,
            AverageMoisture = data.ContainsKey("avg_moist") ? (float)data["avg_moist"] : 0.5f,
            OwnerNationId = data.ContainsKey("owner") ? (string)data["owner"] : "",
        };
    }

    private static HexOverworldTile.TerrainType DefaultTerrainForBiome(BiomeType biome) => biome switch
    {
        BiomeType.Forest => HexOverworldTile.TerrainType.Forest,
        BiomeType.Mountain => HexOverworldTile.TerrainType.Hills,
        BiomeType.Wasteland => HexOverworldTile.TerrainType.Wasteland,
        BiomeType.Swamp => HexOverworldTile.TerrainType.Swamp,
        BiomeType.Tundra => HexOverworldTile.TerrainType.Snow,
        BiomeType.Jungle => HexOverworldTile.TerrainType.Jungle,
        BiomeType.Coastal => HexOverworldTile.TerrainType.ShallowWater,
        _ => HexOverworldTile.TerrainType.Plains,
    };
}
