// EntitySpeedCalculator.cs
// AI 实体移速计算组件 — 从 MovementProcessor 提取独立的速度逻辑
//
// 设计目标:
//   - 供 UI 展示 AI 实体的速度分解（GetSpeedBreakdown）
//   - MovementProcessor 委托到此组件的 CalculateSpeed
//   - 轻量级，不含 Godot Resource 依赖
using BladeHex.Map;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 速度分解报告 — 供 UI 展示 AI 实体每项速度修正因子
/// </summary>
public readonly struct SpeedBreakdown
{
    /// <summary>基础移速 (entity.MoveSpeed)</summary>
    public float Base { get; }

    /// <summary>地形因子 (道路 1.2x, 森林 0.7x 等)</summary>
    public float TerrainFactor { get; }

    /// <summary>ZoC 惩罚因子 (1.0=无惩罚, 0.7=标准惩罚)</summary>
    public float ZocFactor { get; }

    /// <summary>追逐倍率 (Chasing 状态下 AIStrategy 修正)</summary>
    public float ChaseMultiplier { get; }

    /// <summary>天气移速倍率</summary>
    public float WeatherFactor { get; }

    /// <summary>最终移速</summary>
    public float Final { get; }

    /// <summary>当前地形名称（中文）</summary>
    public string TerrainName { get; }

    /// <summary>当前 AI 状态（中文）</summary>
    public string StateName { get; }

    public SpeedBreakdown(float baseSpeed, float terrainFactor, float zocFactor, float chaseMultiplier, float weatherFactor,
        float final, string terrainName, string stateName)
    {
        Base = baseSpeed;
        TerrainFactor = terrainFactor;
        ZocFactor = zocFactor;
        ChaseMultiplier = chaseMultiplier;
        WeatherFactor = weatherFactor;
        Final = final;
        TerrainName = terrainName;
        StateName = stateName;
    }
}

/// <summary>
/// AI 实体移速计算器 — 纯计算，无状态。
/// 从 MovementProcessor.CalculateEffectiveSpeed 提取。
/// 公式: base × terrainFactor × zocFactor × chaseMultiplier × weatherFactor，保底 15%
/// </summary>
public static class EntitySpeedCalculator
{
    /// <summary>
    /// AIStrategy 追逐倍率
    /// </summary>
    public static float GetChaseSpeedMultiplier(AIStrategyEnum strategy) => strategy switch
    {
        AIStrategyEnum.Reckless    => 1.10f,
        AIStrategyEnum.Berserk     => 1.12f,
        AIStrategyEnum.Cunning     => 1.07f,
        AIStrategyEnum.Intimidate  => 1.05f,
        AIStrategyEnum.Tactical    => 1.03f,
        AIStrategyEnum.Territorial => 1.03f,
        AIStrategyEnum.Cautious    => 1.0f,
        AIStrategyEnum.Instinct    => 1.05f,
        _                          => 1.05f,
    };

    /// <summary>
    /// 计算实体最终移速。
    /// 公式: entity.MoveSpeed × 地形因子 × ZoC惩罚 × 追逐倍率 × 天气倍率，保底 15%
    /// </summary>
    public static float CalculateSpeed(
        OverworldEntity entity,
        Vector2 position,
        ChunkManager? chunkManager = null,
        ZoneOfControlManager? zocManager = null,
        float weatherSpeedFactor = 1.0f)
    {
        var terrainQuery = chunkManager != null
            ? OverworldTerrainQuery.ForActiveChunks(chunkManager)
            : null;

        return CalculateSpeed(entity, position, terrainQuery, zocManager, weatherSpeedFactor);
    }

    public static float CalculateSpeed(
        OverworldEntity entity,
        Vector2 position,
        OverworldTerrainQuery? terrainQuery,
        ZoneOfControlManager? zocManager = null,
        float weatherSpeedFactor = 1.0f)
    {
        float speed = entity.MoveSpeed;
        float terrainFactor = GetTerrainFactor(position, terrainQuery);
        float zocFactor = GetZocFactor(entity, position, zocManager);
        float chaseMult = GetAppliedChaseMultiplier(entity);

        speed *= terrainFactor;
        speed *= zocFactor;
        speed *= chaseMult;
        speed *= weatherSpeedFactor;

        return System.Math.Max(speed, entity.MoveSpeed * 0.15f);
    }

    /// <summary>
    /// 获取速度分解报告 — 供 UI 展示各因子
    /// </summary>
    public static SpeedBreakdown GetBreakdown(
        OverworldEntity entity,
        Vector2 position,
        ChunkManager? chunkManager = null,
        ZoneOfControlManager? zocManager = null,
        float weatherSpeedFactor = 1.0f)
    {
        var terrainQuery = chunkManager != null
            ? OverworldTerrainQuery.ForActiveChunks(chunkManager)
            : null;

        return GetBreakdown(entity, position, terrainQuery, zocManager, weatherSpeedFactor);
    }

    public static SpeedBreakdown GetBreakdown(
        OverworldEntity entity,
        Vector2 position,
        OverworldTerrainQuery? terrainQuery,
        ZoneOfControlManager? zocManager = null,
        float weatherSpeedFactor = 1.0f)
    {
        float baseSpeed = entity.MoveSpeed;
        float terrainFactor = GetTerrainFactor(position, terrainQuery);
        float zocFactor = GetZocFactor(entity, position, zocManager);
        float chaseMult = GetAppliedChaseMultiplier(entity);

        float final = baseSpeed;
        final *= terrainFactor;
        final *= zocFactor;
        final *= chaseMult;
        final *= weatherSpeedFactor;
        final = System.Math.Max(final, entity.MoveSpeed * 0.15f);

        string terrainName = "未知";
        if (terrainQuery != null)
            terrainName = terrainQuery.GetTerrainNameAtPixel(position);

        return new SpeedBreakdown(
            baseSpeed,
            terrainFactor,
            zocFactor,
            chaseMult,
            weatherSpeedFactor,
            final,
            terrainName,
            entity.GetStateText()
        );
    }

    // ========================================
    // 因子计算
    // ========================================

    private static float GetTerrainFactor(Vector2 position, OverworldTerrainQuery? terrainQuery)
    {
        return terrainQuery?.GetSpeedFactorAtPixel(position) ?? 1.0f;
    }

    private static float GetZocFactor(OverworldEntity entity, Vector2 position, ZoneOfControlManager? zocManager)
    {
        if (zocManager == null || string.IsNullOrEmpty(entity.Faction))
            return 1.0f;

        var axial = HexOverworldTile.PixelToAxial(position.X, position.Y);
        if (!zocManager.IsInHostileZoc(axial.X, axial.Y, entity.Faction))
            return 1.0f;

        return ZoneOfControlManager.ZocPenalty; // 0.7x
    }

    /// <summary>获取实际生效的追逐倍率（非 Chasing 状态时为 1.0）</summary>
    private static float GetAppliedChaseMultiplier(OverworldEntity entity)
    {
        if (entity.CurrentAIState != OverworldEntity.AIState.Chasing)
            return 1.0f;

        return GetChaseSpeedMultiplier(entity.AIStrategy);
    }
}
