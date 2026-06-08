// BiomeZoneNamer.cs
// 生态区命名器 — 为 BiomeZoneAnalyzer 识别出的生态区分配程序化名称
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Map;

/// <summary>
/// 已命名的生态区 — 扩展 BiomeZone，添加名称和像素坐标
/// </summary>
public class NamedBiomeZone
{
    /// <summary>原始生态区数据</summary>
    public BiomeZone Zone { get; set; } = null!;

    /// <summary>程序化生成的区域名称（英文）</summary>
    public string Name { get; set; } = "";

    /// <summary>程序化生成的区域名称（中文音译）</summary>
    public string NameCN { get; set; } = "";

    /// <summary>几何中心的像素坐标（用于 UI 定位）</summary>
    public Vector2 PixelCenter { get; set; }

    /// <summary>区域边界半径（像素，用于判断玩家是否在区域内）</summary>
    public float RadiusPixels { get; set; }

    /// <summary>区域面积等级（用于 UI 显示大小）</summary>
    public RegionSize SizeClass { get; set; }
}

/// <summary>
/// 区域面积等级
/// </summary>
public enum RegionSize
{
    /// <summary>小型区域（200-500 tiles）</summary>
    Small,
    /// <summary>中型区域（500-1500 tiles）</summary>
    Medium,
    /// <summary>大型区域（1500+ tiles）</summary>
    Large,
}

/// <summary>
/// 生态区命名器 — 为所有生态区分配名称
/// </summary>
public class BiomeZoneNamer
{
    private readonly RegionNameGenerator _nameGenerator;

    /// <summary>坐标 → 生态区映射（用于快速查找）</summary>
    private Dictionary<Vector2I, NamedBiomeZone>? _coordLookup;

    public BiomeZoneNamer(int seed)
    {
        _nameGenerator = new RegionNameGenerator(seed);
    }

    /// <summary>
    /// 为所有生态区分配名称
    /// </summary>
    /// <param name="zones">BiomeZoneAnalyzer 识别出的生态区列表</param>
    /// <returns>已命名的生态区列表</returns>
    public List<NamedBiomeZone> NameAllZones(List<BiomeZone> zones)
    {
        var namedZones = new List<NamedBiomeZone>();

        // 按生态类型分组，用于生成不重复的名称
        var groups = zones.GroupBy(z => z.DominantBiome);

        foreach (var group in groups)
        {
            var biomeZones = group.OrderByDescending(z => z.TileCount).ToList();

            for (int i = 0; i < biomeZones.Count; i++)
            {
                var zone = biomeZones[i];
                var (engName, cnName) = _nameGenerator.GenerateName(zone, i);

                // 计算像素坐标（使用 HexLayoutConfig）
                var layout = HexOverworldTile.GetLayout();
                var pixelCenter = layout.AxialToPixel(zone.Centroid.X, zone.Centroid.Y);

                // 计算区域半径（基于 tile 数量估算）
                float radiusPixels = EstimateRadius(zone);

                // 确定面积等级
                var sizeClass = ClassifySize(zone.TileCount);

                namedZones.Add(new NamedBiomeZone
                {
                    Zone = zone,
                    Name = engName,
                    NameCN = cnName,
                    PixelCenter = pixelCenter,
                    RadiusPixels = radiusPixels,
                    SizeClass = sizeClass,
                });

                GD.Print($"[BiomeZoneNamer] {zone.DominantBiome} #{i}: {engName} / {cnName} ({zone.TileCount} tiles, center={zone.Centroid})");
            }
        }

        GD.Print($"[BiomeZoneNamer] 命名完成: {namedZones.Count} 个区域");

        // 构建坐标查找映射
        BuildCoordLookup(namedZones);

        return namedZones;
    }

    /// <summary>
    /// 构建坐标 → 生态区映射（用于 O(1) 查找）
    /// </summary>
    private void BuildCoordLookup(List<NamedBiomeZone> namedZones)
    {
        _coordLookup = new Dictionary<Vector2I, NamedBiomeZone>();
        foreach (var zone in namedZones)
        {
            foreach (var coord in zone.Zone.TileCoords)
            {
                _coordLookup[coord] = zone;
            }
        }
        GD.Print($"[BiomeZoneNamer] 坐标查找表构建完成: {_coordLookup.Count} 个坐标");
    }

    /// <summary>
    /// 根据轴向坐标查找所在的生态区（O(1) 查找）
    /// </summary>
    public NamedBiomeZone? FindZoneAtCoordFast(Vector2I coord)
    {
        if (_coordLookup == null) return null;
        return _coordLookup.GetValueOrDefault(coord);
    }

    /// <summary>
    /// 估算区域半径（像素）
    /// 假设区域近似圆形，面积 = π * r²
    /// </summary>
    private float EstimateRadius(BiomeZone zone)
    {
        // 每个 tile 约 156 * 156 * sqrt(3)/2 ≈ 21000 平方像素
        float tileArea = HexOverworldTile.HexSize * HexOverworldTile.HexSize * 0.866f;
        float totalArea = zone.TileCount * tileArea;
        float radius = Mathf.Sqrt(totalArea / Mathf.Pi);

        // 最小半径 500 像素，最大 8000 像素
        return Mathf.Clamp(radius, 500f, 8000f);
    }

    /// <summary>
    /// 根据 tile 数量分类面积等级
    /// </summary>
    private RegionSize ClassifySize(int tileCount)
    {
        if (tileCount >= 1500) return RegionSize.Large;
        if (tileCount >= 500) return RegionSize.Medium;
        return RegionSize.Small;
    }

    /// <summary>
    /// 根据像素位置查找所在的生态区
    /// </summary>
    /// <param name="namedZones">已命名的生态区列表</param>
    /// <param name="pixelPos">像素坐标</param>
    /// <returns>所在的生态区，如果没有则返回 null</returns>
    public static NamedBiomeZone? FindZoneAtPosition(List<NamedBiomeZone> namedZones, Vector2 pixelPos)
    {
        NamedBiomeZone? closest = null;
        float closestDist = float.MaxValue;

        foreach (var zone in namedZones)
        {
            float dist = pixelPos.DistanceTo(zone.PixelCenter);

            // 如果在区域半径内
            if (dist <= zone.RadiusPixels)
            {
                // 选择最近的（处理重叠情况）
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = zone;
                }
            }
        }

        return closest;
    }

    /// <summary>
    /// 根据轴向坐标查找所在的生态区
    /// </summary>
    public static NamedBiomeZone? FindZoneAtCoord(List<NamedBiomeZone> namedZones, Vector2I coord)
    {
        foreach (var zone in namedZones)
        {
            if (zone.Zone.TileCoords.Contains(coord))
            {
                return zone;
            }
        }
        return null;
    }
}
