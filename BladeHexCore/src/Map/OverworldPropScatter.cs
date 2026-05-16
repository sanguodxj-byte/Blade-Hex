// OverworldPropScatter.cs
// 大地图场景物体散布算法 — 根据地形类型确定性生成 prop 位置
// 纯 Core 层逻辑：输入 tile 坐标 + 地形类型，输出 List<OverworldPropData>
// 使用 VariantHasher 保证同坐标每次散布一致
using Godot;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 大地图 prop 散布配置 — 每种地形类型对应的 prop 列表和密度
/// </summary>
public sealed class OverworldPropProfile
{
    /// <summary>可用的 prop ID 列表</summary>
    public string[] PropIds = System.Array.Empty<string>();

    /// <summary>每个 tile 生成 prop 的概率（0~1）</summary>
    public float Density = 0.0f;

    /// <summary>每个 tile 最大 prop 数量</summary>
    public int MaxPerTile = 1;

    /// <summary>缩放范围 [min, max]</summary>
    public float ScaleMin = 0.7f;
    public float ScaleMax = 1.3f;

    /// <summary>像素偏移范围（相对 tile 中心的最大偏移）</summary>
    public float OffsetRange = 40.0f;
}

/// <summary>
/// 大地图 prop 散布器 — 确定性生成每个 tile 上的场景物体
/// </summary>
public static class OverworldPropScatter
{
    // ========================================
    // 地形 → prop 配置映射
    // ========================================

    private static readonly Dictionary<HexOverworldTile.TerrainType, OverworldPropProfile> _profiles = new()
    {
        [HexOverworldTile.TerrainType.Forest] = new OverworldPropProfile
        {
            PropIds = new[] { "oak_tree", "birch_tree", "bush" },
            Density = 0.85f, MaxPerTile = 3, ScaleMin = 0.8f, ScaleMax = 1.2f, OffsetRange = 50.0f,
        },
        [HexOverworldTile.TerrainType.DenseForest] = new OverworldPropProfile
        {
            PropIds = new[] { "dark_oak", "pine_dense", "dead_tree", "bush" },
            Density = 0.95f, MaxPerTile = 4, ScaleMin = 0.9f, ScaleMax = 1.4f, OffsetRange = 45.0f,
        },
        [HexOverworldTile.TerrainType.Jungle] = new OverworldPropProfile
        {
            PropIds = new[] { "palm_tree", "jungle_tree", "vine_tree" },
            Density = 0.90f, MaxPerTile = 3, ScaleMin = 0.9f, ScaleMax = 1.3f, OffsetRange = 45.0f,
        },
        [HexOverworldTile.TerrainType.Taiga] = new OverworldPropProfile
        {
            PropIds = new[] { "pine_tree", "spruce_tree", "snow_pine" },
            Density = 0.80f, MaxPerTile = 3, ScaleMin = 0.8f, ScaleMax = 1.2f, OffsetRange = 50.0f,
        },
        [HexOverworldTile.TerrainType.Hills] = new OverworldPropProfile
        {
            PropIds = new[] { "rock_small", "rock_medium", "lone_tree" },
            Density = 0.50f, MaxPerTile = 2, ScaleMin = 0.7f, ScaleMax = 1.1f, OffsetRange = 40.0f,
        },
        [HexOverworldTile.TerrainType.Mountain] = new OverworldPropProfile
        {
            PropIds = new[] { "mountain_peak", "rock_large", "cliff_face" },
            Density = 0.70f, MaxPerTile = 2, ScaleMin = 1.0f, ScaleMax = 1.5f, OffsetRange = 30.0f,
        },
        [HexOverworldTile.TerrainType.MountainSnow] = new OverworldPropProfile
        {
            PropIds = new[] { "snow_peak", "ice_rock", "frozen_cliff" },
            Density = 0.65f, MaxPerTile = 2, ScaleMin = 1.0f, ScaleMax = 1.4f, OffsetRange = 30.0f,
        },
        [HexOverworldTile.TerrainType.Rocky] = new OverworldPropProfile
        {
            PropIds = new[] { "rock_small", "rock_medium", "boulder" },
            Density = 0.60f, MaxPerTile = 2, ScaleMin = 0.7f, ScaleMax = 1.2f, OffsetRange = 35.0f,
        },
        [HexOverworldTile.TerrainType.Grassland] = new OverworldPropProfile
        {
            PropIds = new[] { "lone_tree", "bush", "flower_patch" },
            Density = 0.20f, MaxPerTile = 1, ScaleMin = 0.8f, ScaleMax = 1.1f, OffsetRange = 50.0f,
        },
        [HexOverworldTile.TerrainType.Plains] = new OverworldPropProfile
        {
            PropIds = new[] { "dry_bush", "rock_small" },
            Density = 0.10f, MaxPerTile = 1, ScaleMin = 0.7f, ScaleMax = 1.0f, OffsetRange = 50.0f,
        },
        [HexOverworldTile.TerrainType.Swamp] = new OverworldPropProfile
        {
            PropIds = new[] { "dead_tree", "swamp_stump", "moss_rock" },
            Density = 0.65f, MaxPerTile = 2, ScaleMin = 0.8f, ScaleMax = 1.1f, OffsetRange = 40.0f,
        },
        [HexOverworldTile.TerrainType.Bog] = new OverworldPropProfile
        {
            PropIds = new[] { "dead_tree", "ice_stump", "frozen_reed" },
            Density = 0.50f, MaxPerTile = 2, ScaleMin = 0.7f, ScaleMax = 1.0f, OffsetRange = 40.0f,
        },
        [HexOverworldTile.TerrainType.Snow] = new OverworldPropProfile
        {
            PropIds = new[] { "snow_rock", "dead_bush" },
            Density = 0.15f, MaxPerTile = 1, ScaleMin = 0.7f, ScaleMax = 1.0f, OffsetRange = 45.0f,
        },
        [HexOverworldTile.TerrainType.Sand] = new OverworldPropProfile
        {
            PropIds = new[] { "cactus", "sand_rock", "dry_bush" },
            Density = 0.20f, MaxPerTile = 1, ScaleMin = 0.7f, ScaleMax = 1.1f, OffsetRange = 50.0f,
        },
        [HexOverworldTile.TerrainType.Savanna] = new OverworldPropProfile
        {
            PropIds = new[] { "acacia_tree", "dry_bush", "termite_mound" },
            Density = 0.30f, MaxPerTile = 1, ScaleMin = 0.9f, ScaleMax = 1.3f, OffsetRange = 55.0f,
        },
        [HexOverworldTile.TerrainType.Wasteland] = new OverworldPropProfile
        {
            PropIds = new[] { "dead_bush", "bone_pile", "cracked_rock" },
            Density = 0.25f, MaxPerTile = 1, ScaleMin = 0.7f, ScaleMax = 1.0f, OffsetRange = 40.0f,
        },
    };

    // ========================================
    // 公共 API
    // ========================================

    /// <summary>
    /// 为指定 tile 生成 prop 列表（确定性）。
    /// </summary>
    /// <param name="tileCoord">tile 轴向坐标</param>
    /// <param name="terrain">tile 地形类型</param>
    /// <param name="worldSeed">世界种子（用于全局 salt）</param>
    /// <returns>该 tile 上的 prop 列表（可能为空）</returns>
    public static List<OverworldPropData> Generate(Vector2I tileCoord, HexOverworldTile.TerrainType terrain, int worldSeed = 0)
    {
        var result = new List<OverworldPropData>();

        if (!_profiles.TryGetValue(terrain, out var profile))
            return result;

        if (profile.PropIds.Length == 0 || profile.Density <= 0)
            return result;

        uint salt = (uint)worldSeed ^ 0xDEAD_BEEFu;

        // 第一次哈希：决定是否生成 prop
        float roll = VariantHasher.Pick(tileCoord, 1000, salt) / 1000.0f;
        if (roll > profile.Density)
            return result;

        // 决定数量（1 ~ MaxPerTile）
        int count = 1;
        if (profile.MaxPerTile > 1)
            count = 1 + VariantHasher.Pick(tileCoord, profile.MaxPerTile, salt ^ 0x1111u);

        for (int i = 0; i < count; i++)
        {
            uint iSalt = salt ^ (uint)(i * 7919);

            // 选择 prop ID
            int propIdx = VariantHasher.Pick(tileCoord, profile.PropIds.Length, iSalt ^ 0xAAAAu);
            string propId = profile.PropIds[propIdx];

            // 计算偏移（确定性伪随机）
            float offsetX = (VariantHasher.Pick(tileCoord, 200, iSalt ^ 0xBBBBu) - 100) / 100.0f * profile.OffsetRange;
            float offsetY = (VariantHasher.Pick(tileCoord, 200, iSalt ^ 0xCCCCu) - 100) / 100.0f * profile.OffsetRange;

            // 缩放
            float scaleRange = profile.ScaleMax - profile.ScaleMin;
            float scale = profile.ScaleMin + VariantHasher.Pick(tileCoord, 100, iSalt ^ 0xDDDDu) / 100.0f * scaleRange;

            // 翻转
            bool flipH = VariantHasher.Pick(tileCoord, 2, iSalt ^ 0xEEEEu) == 1;

            result.Add(new OverworldPropData
            {
                PropId = propId,
                TileCoord = tileCoord,
                PixelOffset = new Vector2(offsetX, offsetY),
                Scale = scale,
                FlipH = flipH,
                SortOffset = offsetY * 0.01f, // Y 偏移越大越靠前
            });
        }

        return result;
    }

    /// <summary>获取指定地形的 prop 配置（用于调试/编辑器）</summary>
    public static OverworldPropProfile? GetProfile(HexOverworldTile.TerrainType terrain)
    {
        return _profiles.GetValueOrDefault(terrain);
    }

    /// <summary>注册或覆盖地形的 prop 配置（用于 mod 支持）</summary>
    public static void SetProfile(HexOverworldTile.TerrainType terrain, OverworldPropProfile profile)
    {
        _profiles[terrain] = profile;
    }
}
