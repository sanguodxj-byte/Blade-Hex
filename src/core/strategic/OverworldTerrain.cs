// OverworldTerrain.cs
// 大地图地形类型枚举 — 映射噪声值为地形类型
// 迁移自 GDScript OverworldTerrain.gd
using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图地形类型与工具方法
/// </summary>
public static class OverworldTerrain
{
    public enum Type
    {
        Plains,
        Forest,
        Mountain,
        Swamp,
        Water,
        Road,
        Desert,
    }

    public static Type FromNoise(float noiseValue) => noiseValue switch
    {
        < -0.3f => Type.Water,
        < -0.1f => Type.Swamp,
        < 0.3f => Type.Plains,
        < 0.5f => Type.Forest,
        _ => Type.Mountain,
    };

    public static string GetName(Type terrain) => terrain switch
    {
        Type.Plains => "平原",
        Type.Forest => "森林",
        Type.Mountain => "山地",
        Type.Swamp => "沼泽",
        Type.Water => "水域",
        Type.Road => "道路",
        Type.Desert => "沙漠",
        _ => "未知",
    };

    public static string GetBattleTemplateName(Type terrain) => terrain switch
    {
        Type.Plains => "plain_field",
        Type.Forest => "forest_ambush",
        Type.Mountain => "mountain_pass",
        Type.Swamp => "swamp_battle",
        Type.Water => "coastal_ambush",
        Type.Road => "plain_field",
        Type.Desert => "desert_skirmish",
        _ => "plain_field",
    };
}
