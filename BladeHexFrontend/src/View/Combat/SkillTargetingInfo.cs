// SkillTargetingInfo.cs
// 技能瞄准信息统一描述器 — 封装技能瞄准所需的全部数据，
// 消除 OnActionHovered / HighlightSkillRangeAction / IsSkillTargetCellValid 间的数据获取重复。
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>
/// 技能瞄准信息统一描述器。
/// 通过 SkillTargetingInfo.FromAction(action, caster) 创建，提供：
///   - HighlightColor        目标类型对应的高亮颜色
///   - GetHighlightCells()   应高亮的格子列表
///   - IsCellValid()         单格合法性校验
/// </summary>
public readonly struct SkillTargetingInfo
{
    // ============================================================================
    // 调试开关
    // ============================================================================

    /// <summary>调试日志开关。默认关闭，避免高频日志刷屏。</summary>
    public static bool DebugLogging { get; set; } = false;

    // ============================================================================
    // 数据字段
    // ============================================================================

    /// <summary>原始 action 字符串（"skill_xxx" 或 "career_skill"）</summary>
    public string Action { get; }

    /// <summary>技能效果 ID（如 "whirlwind"）</summary>
    public string SkillEffect { get; }

    /// <summary>技能射程（格数）</summary>
    public int Range { get; }

    /// <summary>目标类型（"Self"/"SingleEnemy"/"RangedSingle"/"AllAdjacent"/"SingleAlly"/"AllAllies"/"RangedAoe"/"AoeSmall"/"AoeCone"）</summary>
    public string TargetType { get; }

    /// <summary>AOE 半径（0=单体）</summary>
    public int AoeRadius { get; }

    /// <summary>AP 消耗</summary>
    public int ActionCost { get; }

    /// <summary>施法者引用（用于 Self / 距离校验）</summary>
    public Unit Caster { get; }

    /// <summary>职业技能引用（普通技能为 null）</summary>
    public CareerSkillData? CareerSkill { get; }

    // ============================================================================
    // 构造（私有 — 通过 FromAction 工厂创建）
    // ============================================================================

    private SkillTargetingInfo(
        string action,
        string skillEffect,
        int range,
        string targetType,
        int aoeRadius,
        int actionCost,
        Unit caster,
        CareerSkillData? careerSkill)
    {
        Action = action;
        SkillEffect = skillEffect;
        Range = range;
        TargetType = targetType;
        AoeRadius = aoeRadius;
        ActionCost = actionCost;
        Caster = caster;
        CareerSkill = careerSkill;
    }

    // ============================================================================
    // 工厂方法
    // ============================================================================

    /// <summary>从 action 字符串解析瞄准信息。返回 null 表示未知 action。</summary>
    public static SkillTargetingInfo? FromAction(string action, Unit caster)
    {
        if (caster == null || !GodotObject.IsInstanceValid(caster))
        {
            if (DebugLogging) GD.Print($"[SkillTarget] FromAction: caster null or invalid (action={action})");
            return null;
        }

        if (action == "career_skill")
        {
            var career = caster.GetCareerSkill();
            if (career == null)
            {
                if (DebugLogging) GD.Print("[SkillTarget] FromAction: career_skill but GetCareerSkill() returned null");
                return null;
            }

            int range = career.EffectParams.GetValueOrDefault("range", 1).AsInt32();
            string targetType = NormalizeCareerTargetType(
                career.EffectParams.GetValueOrDefault("target_type", CareerSkillData.TargetType.SingleEnemy.ToString()));
            if (DebugLogging) GD.Print($"[SkillTarget] FromAction: career_skill effectId={career.EffectId} range={range} target={targetType} ap={career.ApCost}");

            return new SkillTargetingInfo(
                action: action,
                skillEffect: career.EffectId,
                range: range,
                targetType: targetType,
                aoeRadius: 0,
                actionCost: career.ApCost,
                caster: caster,
                careerSkill: career
            );
        }

        if (action.StartsWith("skill_"))
        {
            string skillEffect = action["skill_".Length..];
            int range = SkillRegistry.GetRange(skillEffect);
            string targetType = SkillRegistry.GetTargetType(skillEffect);
            int aoe = SkillRegistry.GetAoeRadius(skillEffect);
            int cost = SkillRegistry.GetActionCost(skillEffect, caster);
            if (!SkillRegistry.CanUseWithEquipment(skillEffect, caster, out _))
                return null;
            if (DebugLogging) GD.Print($"[SkillTarget] FromAction: skill_{skillEffect} range={range} target={targetType} aoe={aoe} ap={cost}");
            return new SkillTargetingInfo(
                action: action,
                skillEffect: skillEffect,
                range: range,
                targetType: targetType,
                aoeRadius: aoe,
                actionCost: cost,
                caster: caster,
                careerSkill: null
            );
        }

        if (DebugLogging) GD.Print($"[SkillTarget] FromAction: unknown action={action}");
        return null;
    }

    // ============================================================================
    // 派生属性
    // ============================================================================

    /// <summary>根据目标类型获取高亮主色</summary>
    public Color HighlightColor
    {
        get
        {
            if (Action == "career_skill")
                return new Color(1.0f, 0.5f, 0.2f, 0.4f); // 职业金橙色

            return TargetType switch
            {
                "SingleEnemy" or "RangedSingle" => new Color(0.9f, 0.3f, 0.2f, 0.35f),   // 红：单体敌人
                "RangedAoe" or "AoeSmall" or "AoeCone" => new Color(0.9f, 0.5f, 0.1f, 0.35f), // 橙：AOE
                "Ground" => new Color(0.3f, 0.6f, 1.0f, 0.35f), // 蓝：地面位移
                "AllAdjacent" => new Color(0.9f, 0.7f, 0.2f, 0.4f),  // 黄：周围
                "SingleAlly" or "AllAllies" => new Color(0.2f, 0.8f, 0.4f, 0.35f),  // 绿：友军
                _ => new Color(0.9f, 0.7f, 0.2f, 0.4f),  // 默认黄
            };
        }
    }

    /// <summary>Self / 施法者自身高亮专用颜色（蓝色）</summary>
    public static Color SelfHighlightColor => new(0.3f, 0.6f, 1.0f, 0.5f);

    // ============================================================================
    // 高亮辅助
    // ============================================================================

    /// <summary>
    /// 获取应当高亮的格子列表（不修改格子状态，只收集引用）。
    /// 返回的列表中，第一个格子为施法者自身格（如有），其余为范围格。
    /// </summary>
    public void GetHighlightCells(HexGrid grid, List<HexCell> results)
    {
        if (grid == null || results == null || !GodotObject.IsInstanceValid(Caster)) return;
        var casterPos = Caster.GridPos;

        if (TargetType == "Self" || Range == 0)
        {
            var selfC = grid.GetCell(casterPos.X, casterPos.Y);
            if (selfC != null) results.Add(selfC);
        }
        else if (TargetType == "AllAdjacent")
        {
            var selfC = grid.GetCell(casterPos.X, casterPos.Y);
            if (selfC != null) results.Add(selfC);
            foreach (var n in HexUtils.GetNeighbors(casterPos.X, casterPos.Y))
            {
                var nc = grid.GetCell(n.X, n.Y);
                if (nc != null) results.Add(nc);
            }
        }
        else
        {
            foreach (var coord in grid.GetCellsInRange(casterPos.X, casterPos.Y, Range))
            {
                var c = grid.GetCell(coord.X, coord.Y);
                if (c != null) results.Add(c);
            }
        }
    }

    /// <summary>
    /// 对 HexGrid 执行高亮（修改格子状态）。不负责清空已有高亮。
    /// Self 类 / AllAdjacent 的施法者格使用 SelfHighlightColor（蓝色），其余格使用 HighlightColor。
    /// </summary>
    public void ApplyHighlight(HexGrid grid, List<HexCell> highlightedCells)
    {
        var cells = new List<HexCell>();
        GetHighlightCells(grid, cells);

        if (!GodotObject.IsInstanceValid(Caster)) return;
        var casterPos = Caster.GridPos;

        foreach (var cell in cells)
        {
            if (CombatAttackRules.IsMeleeSkill(SkillEffect)
                && cell.Occupant != null
                && CombatAttackRules.IsMeleeElevationBlocked(Caster, cell.Occupant, grid))
            {
                continue;
            }

            // Self 格 / AllAdjacent 的施法者格使用蓝色
            if (cell.GridPos == casterPos && (TargetType == "Self" || TargetType == "AllAdjacent" || Range == 0))
            {
                cell.SetHighlight(true, SelfHighlightColor);
            }
            else
            {
                cell.SetHighlight(true, HighlightColor);
            }
            highlightedCells.Add(cell);
        }
    }

    // ============================================================================
    // 校验辅助
    // ============================================================================

    /// <summary>
    /// 校验目标格在技能瞄准模式下是否合法。
    /// 不检查高亮状态（避免 UI 事件意外清高亮后无法施法）。
    /// </summary>
    public bool IsCellValid(HexCell cell, CombatManager combatManager)
    {
        if (cell == null || !GodotObject.IsInstanceValid(Caster)) return false;
        var casterPos = Caster.GridPos;

        int dist = HexUtils.AxialDistance(casterPos, cell.GridPos);
        if (dist > Range) return false;

        if (CombatAttackRules.IsMeleeSkill(SkillEffect)
            && cell.Occupant != null
            && CombatAttackRules.IsMeleeElevationBlocked(Caster, cell.Occupant, combatManager.CurrentGrid))
        {
            return false;
        }

        if (SkillEffect == "fearless_charge" && !IsFearlessChargeCellValid(cell, combatManager))
            return false;
        if (SkillEffect == "evasive_roll" && !IsEmptyPassableCell(cell))
            return false;
        if (SkillEffect == "shadow_lunge" && !HasPassableAdjacentLanding(cell, combatManager))
            return false;

        return TargetType switch
        {
            "Self" => cell.Occupant == Caster,
            "SingleEnemy" or "RangedSingle" => cell.Occupant != null && combatManager.EnemyUnits.Contains(cell.Occupant),
            "SingleAlly" => cell.Occupant != null && combatManager.PlayerUnits.Contains(cell.Occupant),
            "Ground" => IsEmptyPassableCell(cell),
            "AllAllies" => true,
            "RangedAoe" or "AoeSmall" or "AoeCone" => true,
            "AllAdjacent" => dist <= 1,
            _ => true,
        };
    }

    private bool IsFearlessChargeCellValid(HexCell targetCell, CombatManager combatManager)
    {
        var grid = combatManager.CurrentGrid;
        if (grid == null || targetCell.Occupant == null) return false;

        int distance = HexUtils.AxialDistance(Caster.GridPos, targetCell.GridPos);
        if (distance <= 1) return false;

        for (int dir = 0; dir < HexUtils.Directions.Length; dir++)
        {
            var step = HexUtils.Directions[dir];
            var current = Caster.GridPos;
            for (int i = 0; i < distance; i++)
                current += step;

            if (current != targetCell.GridPos) continue;

            var reverse = HexUtils.Directions[(dir + 3) % 6];
            var landing = targetCell.GridPos + reverse;
            var landingCell = grid.GetCell(landing.X, landing.Y);
            return landingCell != null
                && landingCell.Occupant == null
                && (landingCell.Data == null || landingCell.Data.isPassable);
        }

        return false;
    }

    private static bool IsEmptyPassableCell(HexCell cell)
        => cell.Occupant == null && (cell.Data == null || cell.Data.isPassable);

    private bool HasPassableAdjacentLanding(HexCell targetCell, CombatManager combatManager)
    {
        var grid = combatManager.CurrentGrid;
        if (grid == null || targetCell.Occupant == null) return false;

        foreach (var pos in HexUtils.GetNeighbors(targetCell.GridPos.X, targetCell.GridPos.Y))
        {
            var landingCell = grid.GetCell(pos.X, pos.Y);
            if (landingCell != null && IsEmptyPassableCell(landingCell))
                return true;
        }

        return false;
    }

    // ============================================================================
    // AOE 预览
    // ============================================================================

    /// <summary>该技能是否为 AOE 类型（范围伤害/治疗）</summary>
    public bool IsAoe => TargetType is "RangedAoe" or "AoeSmall" or "AoeCone" or "AllAdjacent";

    /// <summary>获取 AOE 半径（格数）。0=单体。</summary>
    public int GetAoeRadius()
    {
        if (AoeRadius > 0) return AoeRadius;
        return TargetType switch
        {
            "AllAdjacent" => 1,
            "AoeSmall" => 1,
            "RangedAoe" => 2,
            _ => 0,
        };
    }

    /// <summary>
    /// 获取以 center 为中心的 AOE 范围内的所有格子。
    /// 用于悬浮预览：显示技能会影响哪些格子。
    /// </summary>
    public void GetAoeCells(HexGrid grid, Vector2I center, List<HexCell> results)
    {
        if (grid == null || results == null) return;

        int radius = GetAoeRadius();
        if (radius <= 0)
        {
            // 单体：只返回目标格
            var singleCell = grid.GetCell(center.X, center.Y);
            if (singleCell != null) results.Add(singleCell);
            return;
        }

        // 球形 AOE：使用 SpellShapeResolver
        var coords = Data.SpellShapeResolver.ShapeSphere(center, radius, c => grid.GetCell(c.X, c.Y) != null);
        foreach (var coord in coords)
        {
            var cell = grid.GetCell(coord.X, coord.Y);
            if (cell != null) results.Add(cell);
        }
    }

    /// <summary>
    /// 获取以 center 为中心的 AOE 范围内的所有格子坐标。
    /// 返回坐标数组，用于不依赖 HexCell 引用的场景。
    /// </summary>
    public Vector2I[] GetAoeCellCoords(HexGrid grid, Vector2I center)
    {
        if (grid == null) return [];

        int radius = GetAoeRadius();
        if (radius <= 0) return [center];

        return Data.SpellShapeResolver.ShapeSphere(center, radius, c => grid.GetCell(c.X, c.Y) != null);
    }

    private static string NormalizeCareerTargetType(Variant value)
    {
        if (value.VariantType == Variant.Type.Int)
            return ((CareerSkillData.TargetType)value.AsInt32()).ToString();

        string text = value.AsString();
        if (int.TryParse(text, out int targetValue))
            return ((CareerSkillData.TargetType)targetValue).ToString();

        return string.IsNullOrWhiteSpace(text)
            ? CareerSkillData.TargetType.SingleEnemy.ToString()
            : text;
    }
}
