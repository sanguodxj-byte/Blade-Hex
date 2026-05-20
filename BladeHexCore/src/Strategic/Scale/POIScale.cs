// POIScale.cs
// 比例尺统一 — 4 档 POI 尺度枚举与参数 profile
//
// 见 .kiro/specs/scale-unification/design.md
//
// 每档 Scale 同时绑定：
//   - 视觉 marker size
//   - 灯光 range / energy
//   - 战斗 hex 半径 N
//   - 默认 BattleSize
//   - 与其他 POI 的最小 footprint hex 距离

using System;

namespace BladeHex.Strategic;

/// <summary>POI 尺度档位 — 决定视觉/互动/战斗参数</summary>
public enum POIScale
{
    Tiny,    // 1 hex,  ~250 m  (哨塔 / 农庄 / 矿场)
    Small,   // 3 hex,  ~500 m  (村庄 / 营地 / 古墓)
    Medium,  // 5 hex,  ~1 km   (市镇 / 山城 / 大营地)
    Large,   // 7 hex,  ~1.5 km (大型城市 / 都城 / 要塞)
}

/// <summary>POI 单档 Scale 对应的参数集合</summary>
public readonly struct POIScaleProfile
{
    /// <summary>POI sprite / mesh 视觉尺寸</summary>
    public float MarkerSize { get; init; }

    /// <summary>灯光辐射范围（pixel/world unit）</summary>
    public float LightRange { get; init; }

    /// <summary>灯光能量强度</summary>
    public float LightEnergy { get; init; }

    /// <summary>战斗六边形 grid 半径 N（cell 数 = 1 + 3·N·(N+1)）</summary>
    public int BattleHexRadius { get; init; }

    /// <summary>默认战斗规模（preset 可 override）</summary>
    public BattleContext.BattleSize BattleSize { get; init; }

    /// <summary>与其他 POI 的最小 footprint hex 距离</summary>
    public int MinSpawnDistanceHex { get; init; }

    /// <summary>
    /// R1#2 (2026-05-17) 战斗采样圈数：从 footprint 整体外扩多少层 hex 边。
    /// Tiny=1, Small=1, Medium=2, Large=3。让大型战斗反映"更广阔的周边地区"，而不是把同一圈邻居拉伸成更大地图。
    /// </summary>
    public int SamplingRingCount { get; init; }

    public POIScaleProfile(
        float markerSize,
        float lightRange,
        float lightEnergy,
        int battleHexRadius,
        BattleContext.BattleSize battleSize,
        int minSpawnDistanceHex,
        int samplingRingCount)
    {
        MarkerSize = markerSize;
        LightRange = lightRange;
        LightEnergy = lightEnergy;
        BattleHexRadius = battleHexRadius;
        BattleSize = battleSize;
        MinSpawnDistanceHex = minSpawnDistanceHex;
        SamplingRingCount = samplingRingCount;
    }
}

/// <summary>POIScale → POIScaleProfile 查表</summary>
public static class POIScaleTable
{
    public static POIScaleProfile Get(POIScale scale) => scale switch
    {
        POIScale.Tiny => new POIScaleProfile(
            markerSize: 0.35f, lightRange: 1.5f, lightEnergy: 0.4f,
            battleHexRadius: 7, battleSize: BattleContext.BattleSize.Mercenary,
            minSpawnDistanceHex: 1, samplingRingCount: 1),

        POIScale.Small => new POIScaleProfile(
            markerSize: 0.45f, lightRange: 2.0f, lightEnergy: 0.6f,
            battleHexRadius: 8, battleSize: BattleContext.BattleSize.Mercenary,
            minSpawnDistanceHex: 3, samplingRingCount: 1),

        POIScale.Medium => new POIScaleProfile(
            markerSize: 0.6f, lightRange: 3.0f, lightEnergy: 0.9f,
            battleHexRadius: 11, battleSize: BattleContext.BattleSize.Knight,
            minSpawnDistanceHex: 5, samplingRingCount: 2),

        POIScale.Large => new POIScaleProfile(
            markerSize: 0.8f, lightRange: 4.0f, lightEnergy: 1.2f,
            battleHexRadius: 14, battleSize: BattleContext.BattleSize.Lord,
            minSpawnDistanceHex: 7, samplingRingCount: 3),

        _ => Get(POIScale.Tiny),
    };

    /// <summary>战斗 cell 总数 = 1 + 3·N·(N+1)</summary>
    public static int BattleCellCount(POIScale scale)
    {
        int n = Get(scale).BattleHexRadius;
        return 1 + 3 * n * (n + 1);
    }
}
