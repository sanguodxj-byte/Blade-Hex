// TerrainVisualProfile.cs
// SSOT — 地形视觉画像：Core 持 string ID，View 解析为实际资源
//
// 背景：大地图（2D 平面，羊皮纸画风）与战斗地图（3D 六棱柱 + 贴 2D 写实纹理 + 立牌）
// 使用两套完全独立的纹理资产，但共享同一个 TerrainType 枚举。
// 本 profile 把"一种地形在两边各用哪个 key"集中在一处，消除双份 switch。
//
// Core 只持 string ID，具体的 Texture2D / Sprite3D / ShaderMaterial 由 View 层
// TerrainVisualRegistry + ResourceRegistry 在运行时解析。
using Godot;
using System.Collections.Generic;

namespace BladeHex.Map;

/// <summary>
/// 单个地形类型的视觉配置（纯数据，无渲染类型）
/// </summary>
public sealed class TerrainVisualProfile
{
    // ========================================
    // 身份
    // ========================================

    /// <summary>绑定的大地图地形类型</summary>
    public HexOverworldTile.TerrainType Terrain;

    /// <summary>调试/日志用的可读名</summary>
    public string DisplayName = "";

    // ========================================
    // 大地图资产（2D 平面 / 羊皮纸画风）
    // ========================================

    /// <summary>
    /// 大地图六边形贴图 key（新资产，未来放在 overworld/）
    /// Texture path: res://src/assets/tiles/overworld/{OverworldKey}_{variant}.png
    /// </summary>
    public string OverworldKey = "grassland";

    /// <summary>大地图贴图变体数量（≥ 1）</summary>
    public int OverworldVariantCount = 1;

    /// <summary>
    /// 旧资产贴图 key（迁移期回退）
    /// 指向 res://src/assets/tiles/hex_terrain/{LegacyOverworldKey}_{variant}.png
    /// 与 OverworldKey 不同的原因：新命名更规范，但旧资产文件名保留。
    /// 渲染器加载顺序：先尝试 OverworldKey (新路径) → 失败则回退 LegacyOverworldKey (旧路径)
    /// </summary>
    public string LegacyOverworldKey = "grassland";

    /// <summary>旧资产变体数量</summary>
    public int LegacyOverworldVariantCount = 1;

    // ========================================
    // 战斗地图资产（3D 六棱柱 + 贴 2D 写实纹理）
    // ========================================

    /// <summary>
    /// 战斗地图顶面贴图 key（tileable 2D 写实材质）
    /// Texture path: res://src/assets/tiles/battle_ground/tops/{BattleTopKey}_{variant}.png
    /// </summary>
    public string BattleTopKey = "grassland_top";

    /// <summary>战斗顶面贴图变体数量（≥ 1）</summary>
    public int BattleTopVariantCount = 1;

    /// <summary>
    /// 战斗地图六棱柱侧面（悬崖）贴图 key
    /// Texture path: res://src/assets/tiles/battle_ground/cliffs/{BattleCliffKey}.png
    /// </summary>
    public string BattleCliffKey = "cliff_dirt";

    /// <summary>
    /// 装饰 prop pack id 列表（指向 BattlePropRegistry 里的立牌资源集合）
    /// 例如 ["oak_tree", "pine_tree", "mossy_rock"]
    /// </summary>
    public List<string> BattlePropPack = new();

    /// <summary>每格 prop 出现的概率（0-1），战斗地图生成时参考</summary>
    public float PropDensity = 0.0f;

    // ========================================
    // 色调锚点
    // ========================================

    /// <summary>
    /// 地形主色（sRGB）。两套纹理美术各自绘制时，平均色调需落在此值 ±10% 范围内。
    /// 用途：占位色显示、minimap、调试、以及美术校对。
    /// </summary>
    public Color DominantColor = new Color(0.5f, 0.5f, 0.5f);

    /// <summary>程序化shader调色板：暗影/墨线色（最低明度）</summary>
    public Color PaletteDark = new Color(0.30f, 0.22f, 0.15f);

    /// <summary>程序化shader调色板：高光色（最高明度）</summary>
    public Color PaletteLight = new Color(0.75f, 0.65f, 0.45f);

    /// <summary>程序化shader地形纹理风格（0=平原, 1=森林, 2=水域, 3=山地, 4=沙漠, 5=雪地, 6=道路, 7=沼泽）</summary>
    public int PatternType = 0;

    // ========================================
    // 便捷方法
    // ========================================

    /// <summary>获取给定大地图坐标应使用的 overworld 变体索引（确定性）</summary>
    public int GetOverworldVariant(Vector2I worldCoord)
    {
        return VariantHasher.Pick(worldCoord, OverworldVariantCount);
    }

    /// <summary>获取旧资产变体索引</summary>
    public int GetLegacyVariant(Vector2I worldCoord)
    {
        return VariantHasher.Pick(worldCoord, LegacyOverworldVariantCount);
    }

    /// <summary>
    /// 获取给定大地图坐标应使用的战斗顶面变体索引（确定性）
    /// 战斗发生时传入大地图坐标 (而非战斗局部坐标)，保证
    /// "大地图上是 forest_1，进战斗后中心格也是 forest_1"
    /// </summary>
    public int GetBattleTopVariant(Vector2I worldCoord)
    {
        return VariantHasher.Pick(worldCoord, BattleTopVariantCount);
    }
}
