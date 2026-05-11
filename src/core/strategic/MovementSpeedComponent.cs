using Godot;
using System;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图行进移动速度组件 — 汇总所有速度修正因子，输出最终移动速度
/// </summary>
public partial class MovementSpeedComponent : RefCounted
{
    // ========================================
    // 配置
    // ========================================

    [Export] public float BaseSpeed = 300.0f;
    [Export] public float NightSpeedFactor = 0.75f;
    [Export] public float EncumbrancePenalty = 0.3f;
    [Export] public float MountBaseBonus = 1.25f;
    [Export] public float MountSpeedPerBonus = 0.05f;

    // ========================================
    // 依赖引用
    // ========================================

    public HexOverworldGrid? HexGridRef = null;
    public EconomyManager? EconomyManagerRef = null;
    public UnitData? UnitDataRef = null;

    // ========================================
    // 速度计算
    // ========================================

    /// <summary>计算最终移动速度（像素/秒）</summary>
    public float CalculateSpeed(Vector2 position)
    {
        float speed = BaseSpeed;

        // 1. 地形修正
        speed *= GetTerrainFactor(position);

        // 2. 季节修正
        speed *= GetSeasonFactor();

        // 3. 昼夜修正
        speed *= GetDayNightFactor();

        // 4. 负重修正
        speed *= GetEncumbranceFactor();

        // 5. 坐骑修正
        speed *= GetMountFactor();

        // 6. 技能盘修正
        speed *= GetSkillFactor();

        // 保底：最低不低于基础速度的20%
        return Mathf.Max(speed, BaseSpeed * 0.2f);
    }

    /// <summary>获取速度分解报告</summary>
    public Godot.Collections.Dictionary GetSpeedBreakdown(Vector2 position)
    {
        var breakdown = new Godot.Collections.Dictionary
        {
            { "base", BaseSpeed },
            { "terrain", GetTerrainFactor(position) },
            { "season", GetSeasonFactor() },
            { "day_night", GetDayNightFactor() },
            { "encumbrance", GetEncumbranceFactor() },
            { "mount", GetMountFactor() },
            { "skill", GetSkillFactor() },
            { "final", CalculateSpeed(position) }
        };

        // 地形名称获取逻辑
        int terrain = SampleTerrain(position);
        breakdown["terrain_name"] = terrain switch
        {
            0 => "平原", // Type.PLAINS
            1 => "森林", // Type.FOREST
            2 => "山地", // Type.MOUNTAIN
            3 => "沼泽", // Type.SWAMP
            4 => "水域", // Type.WATER
            5 => "道路", // Type.ROAD
            6 => "沙漠", // Type.DESERT
            _ => "未知"
        };

        return breakdown;
    }

    // ========================================
    // 各修正因子
    // ========================================

    private float GetTerrainFactor(Vector2 position)
    {
        int terrain = SampleTerrain(position);
        // 这里需要 OverworldTerrain.get_move_speed_multiplier 的逻辑
        return terrain switch
        {
            0 => 1.0f, // PLAINS
            1 => 0.7f, // FOREST
            2 => 0.5f, // MOUNTAIN
            3 => 0.5f, // SWAMP
            4 => 0.3f, // WATER
            5 => 1.5f, // ROAD
            6 => 0.8f, // DESERT
            _ => 1.0f
        };
    }

    private float GetSeasonFactor()
    {
        if (EconomyManagerRef == null) return 1.0f;
        if (EconomyManagerRef.GetSeason() == EconomyManager.Season.Winter) return 0.5f;
        return 1.0f;
    }

    private float GetDayNightFactor()
    {
        if (EconomyManagerRef == null) return 1.0f;
        float hour = EconomyManagerRef.CurrentHour;
        if (hour >= 18.0f || hour < 6.0f) return NightSpeedFactor;
        return 1.0f;
    }

    private float GetEncumbranceFactor()
    {
        if (UnitDataRef == null || EconomyManagerRef == null) return 1.0f;
        int inventoryCount = EconomyManagerRef.PlayerInventory.Count;
        if (inventoryCount <= 10) return 1.0f;
        float overload = Mathf.Min((inventoryCount - 10) / 20.0f, 1.0f);
        return 1.0f - overload * EncumbrancePenalty;
    }

    private float GetMountFactor()
    {
        if (UnitDataRef == null) return 1.0f;
        if (UnitDataRef.IsMounted && UnitDataRef.Mount != null)
        {
            return MountBaseBonus + UnitDataRef.Mount.SpeedBonus * MountSpeedPerBonus;
        }
        return 1.0f;
    }

    private float GetSkillFactor()
    {
        if (UnitDataRef == null) return 1.0f;

        float factor = 1.0f;

        // TODO: 等待 Phase 5.3 SkillTreeManager 迁移后完善
        // var mgr = SkillTreeManager.GetInstance();
        // if (mgr != null && UnitDataRef.CharacterId >= 0) { ... }

        // Keystone 惩罚逻辑
        int keystonePenalty = 0;
        if (UnitDataRef.SkillTreeData.ContainsKey("diamond_body") && (bool)UnitDataRef.SkillTreeData["diamond_body"])
            keystonePenalty += 2;
        if (UnitDataRef.SkillTreeData.ContainsKey("life_spring") && (bool)UnitDataRef.SkillTreeData["life_spring"])
            keystonePenalty += 1;

        factor -= keystonePenalty * 0.1f;

        return Mathf.Max(factor, 0.3f);
    }

    private int SampleTerrain(Vector2 position)
    {
        if (HexGridRef != null)
        {
            return HexGridRef.SampleTerrainAtPixel(position.X, position.Y);
        }
        return 0; // Type.PLAINS
    }
}
