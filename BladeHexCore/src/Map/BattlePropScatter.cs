// BattlePropScatter.cs
// 战斗地图立牌散布算法 — 按 TerrainVisualProfile.PropDensity 确定性采样
//
// Core 层纯逻辑：输入 hex 坐标 + terrain type，输出 List<BattlePropPlacement>
// 使用 VariantHasher 保证同坐标每次散布一致（战斗可重开）
using System.Collections.Generic;
using Godot;
using BladeHex.Data;

namespace BladeHex.Map;

public static class BattlePropScatter
{
    /// <summary>
    /// 为一个战斗格子生成 prop 列表
    /// </summary>
    /// <param name="worldCoord">大地图上该格对应的全局坐标（保证确定性的种子）</param>
    /// <param name="terrain">战斗格的地形</param>
    /// <param name="rngSalt">场景级 salt，允许同坐标在不同战斗模板下有不同散布</param>
    public static List<BattlePropPlacement> Generate(
        Vector2I worldCoord,
        BattleCellData.TerrainType terrain,
        uint rngSalt = 0)
    {
        var profile = BattleTerrainBridge.GetProfile(terrain);
        var result = new List<BattlePropPlacement>();

        if (profile.BattlePropPack.Count == 0 || profile.PropDensity <= 0f)
            return result;

        // 判定是否生成 prop：把 PropDensity 映射到 [0, 256) 区间
        int gate = Mathf.Clamp((int)(profile.PropDensity * 256f), 0, 255);
        int roll = VariantHasher.Pick(worldCoord, 256, rngSalt ^ 0xA1B2C3D4u);
        if (roll >= gate) return result;

        // 从 pack 里挑一个
        int propIdx = VariantHasher.Pick(worldCoord, profile.BattlePropPack.Count, rngSalt ^ 0x11223344u);
        string propId = profile.BattlePropPack[propIdx];

        // 散布参数（偏移 / 朝向 / 缩放）都从 hash 派生
        float offsetAngle = VariantHasher.Pick(worldCoord, 360, rngSalt ^ 0xDEADBEEFu) * Mathf.Pi / 180f;
        float offsetMag = (VariantHasher.Pick(worldCoord, 40, rngSalt ^ 0xCAFEBABEu) - 20) * 1.0f; // [-20, 20) units
        float yaw = VariantHasher.Pick(worldCoord, 360, rngSalt ^ 0x01020304u);
        float scale = 0.85f + VariantHasher.Pick(worldCoord, 30, rngSalt ^ 0x0F0F0F0Fu) * 0.01f; // [0.85, 1.15)

        result.Add(new BattlePropPlacement
        {
            PropId = propId,
            LocalOffset = new Vector3(
                Mathf.Cos(offsetAngle) * offsetMag,
                0f,
                Mathf.Sin(offsetAngle) * offsetMag
            ),
            YawDegrees = yaw,
            Scale = scale,
            ProvidesHalfCover = IsHalfCoverProp(propId),
            ProvidesFullCover = IsFullCoverProp(propId),
            BlocksLineOfSight = IsLosBlockerProp(propId),
        });

        return result;
    }

    // prop 的战术属性（Core 层表；View 层只用它选择贴图）
    // 约定：prop_id 命名暗示类型 — *_tree / *_bush / *_rock / *_log / ...
    private static bool IsHalfCoverProp(string propId)
    {
        return propId.Contains("bush") || propId.Contains("reed") || propId.Contains("fern")
            || propId.Contains("rock") && !propId.Contains("small_rock")
            || propId.Contains("log") || propId.Contains("boulder");
    }

    private static bool IsFullCoverProp(string propId)
    {
        return propId.Contains("tree") || propId.Contains("cliff_chunk");
    }

    private static bool IsLosBlockerProp(string propId)
    {
        return IsFullCoverProp(propId);
    }
}
