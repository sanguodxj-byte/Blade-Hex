using Godot;
using System;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.View.Map;

namespace BladeHex.Strategic;

/// <summary>
/// 大地图行进移动速度组件 — 汇总所有速度修正因子，输出最终移动速度
/// </summary>
[GlobalClass]
public partial class MovementSpeedComponent : Resource
{
    // ========================================
    // 配置
    // ========================================

    [Export] public float BaseSpeed { get; set; } = 300.0f;
    [Export] public float NightSpeedFactor { get; set; } = 0.75f;
    [Export] public float EncumbrancePenalty { get; set; } = 0.3f;
    [Export] public float MountBaseBonus { get; set; } = 1.25f;
    [Export] public float MountSpeedPerBonus { get; set; } = 0.05f;

    // ========================================
    // 依赖引用
    // ========================================

    public OverworldMapAccess? MapAccess = null;
    public HexOverworldGrid? HexGridRef = null;
    public EconomyManager? EconomyManagerRef = null;
    public UnitData? UnitDataRef = null;

    /// <summary>Chunk 模式下的 ChunkManager 引用（优先于 HexGridRef）</summary>
    public ChunkManager? ChunkManagerRef = null;

    /// <summary>控制区管理器引用</summary>
    public ZoneOfControlManager? ZocManagerRef = null;

    /// <summary>玩家阵营 ID（用于 ZoC 敌对判定）</summary>
    public string PlayerFaction { get; set; } = "";

    /// <summary>天气移速修正因子（由外部天气系统设置，1.0=无影响）</summary>
    public float WeatherSpeedFactor { get; set; } = 1.0f;

    // ========================================
    // 速度计算
    // ========================================

    /// <summary>计算最终移动速度（像素/秒）</summary>
    public float CalculateSpeed(Vector2 position)
    {
        float speed = BaseSpeed;

        // 1. 地形修正（使用 TerrainCostTable 统一数据源）
        speed *= GetTerrainFactor(position);

        // 2. 昼夜修正
        speed *= GetDayNightFactor();

        // 4. 负重修正
        speed *= GetEncumbranceFactor();

        // 5. 坐骑修正
        speed *= GetMountFactor();

        // 6. 技能盘修正
        speed *= GetSkillFactor();

        // 7. 天气修正
        speed *= WeatherSpeedFactor;

        // 8. 敌对 ZoC 惩罚（最后施加，不叠加）
        speed *= GetZocFactor(position);

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
            { "day_night", GetDayNightFactor() },
            { "encumbrance", GetEncumbranceFactor() },
            { "mount", GetMountFactor() },
            { "skill", GetSkillFactor() },
            { "weather", WeatherSpeedFactor },
            { "zoc_penalty", GetZocFactor(position) },
            { "final", CalculateSpeed(position) }
        };

        // 地形名称
        var tile = GetTileAtPosition(position);
        if (tile != null)
            breakdown["terrain_name"] = HexOverworldTile.TerrainToString(tile.Terrain);
        else
            breakdown["terrain_name"] = "未知";

        return breakdown;
    }

    // ========================================
    // 各修正因子
    // ========================================

    private float GetTerrainFactor(Vector2 position)
    {
        var tile = GetTileAtPosition(position);
        if (tile == null) return 1.0f;

        // 使用 TerrainCostTable 统一数据源
        return TerrainCostTable.GetSpeedFactor(tile);
    }

    /// <summary>获取 ZoC 惩罚因子（1.0=无惩罚, 0.7=标准惩罚, 0.85=技能减轻）</summary>
    private float GetZocFactor(Vector2 position)
    {
        if (ZocManagerRef == null || string.IsNullOrEmpty(PlayerFaction))
            return 1.0f;

        var axial = HexOverworldTile.PixelToAxial(position.X, position.Y);
        if (!ZocManagerRef.IsInHostileZoc(axial.X, axial.Y, PlayerFaction))
            return 1.0f;

        // 检查是否有 ZoC 抗性技能
        bool hasResistance = HasZocResistanceSkill();
        return ZoneOfControlManager.GetEffectivePenalty(hasResistance);
    }

    /// <summary>检查是否拥有 ZoC 抗性技能</summary>
    private bool HasZocResistanceSkill()
    {
        if (UnitDataRef == null) return false;
        return HasSkillTreeFlag("zoc_resistance");
    }

    /// <summary>获取指定像素位置的 tile（优先统一地图访问器，回退旧引用）</summary>
    private HexOverworldTile? GetTileAtPosition(Vector2 position)
    {
        if (MapAccess != null)
            return MapAccess.GetActiveTileAtPixel(position);

        if (ChunkManagerRef != null || HexGridRef != null)
            return new OverworldMapAccess(ChunkManagerRef, HexGridRef).GetActiveTileAtPixel(position);

        return null;
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
        int inventoryCount = EconomyManagerRef.PlayerInventoryCount;
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

        // Keystone 惩罚逻辑
        int keystonePenalty = 0;
        if (HasSkillTreeFlag("iron_body", "diamond_body"))
            keystonePenalty += 2;
        if (HasSkillTreeFlag("life_spring"))
            keystonePenalty += 1;

        factor -= keystonePenalty * 0.1f;

        return Mathf.Max(factor, 0.3f);
    }

    private bool HasSkillTreeFlag(string effectId, params string[] legacyEffectIds)
    {
        if (UnitDataRef == null) return false;
        if (HasSkillTreeFlagValue(effectId)) return true;

        foreach (var legacyId in legacyEffectIds)
        {
            if (HasSkillTreeFlagValue(legacyId))
                return true;
        }

        return false;
    }

    private bool HasSkillTreeFlagValue(string effectId)
    {
        return UnitDataRef != null
            && UnitDataRef.SkillTreeData.ContainsKey(effectId)
            && UnitDataRef.SkillTreeData[effectId].AsBool();
    }
}
