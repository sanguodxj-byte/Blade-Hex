using Godot;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;

namespace BladeHex.Combat;

/// <summary>Shared hard attack-legality rules used by UI, AI and final resolution.</summary>
public static class CombatAttackRules
{
    public const int MeleeElevationBlockThreshold = 2;
    public const string MeleeElevationBlockedReason = "高度差过大，无法近战攻击。";

    public static bool IsMeleeWeaponAttack(Unit attacker)
    {
        if (attacker == null || !GodotObject.IsInstanceValid(attacker)) return false;
        var weapon = attacker.GetMainHand() as WeaponData;
        return weapon == null || (!weapon.IsRanged && !weapon.IsThrowing);
    }

    public static bool IsMeleeSkill(string skillEffect)
    {
        return !string.IsNullOrEmpty(skillEffect) && SkillRegistry.IsMeleeActive(skillEffect);
    }

    public static bool IsMeleeCareerSkill(CareerSkillData? skill)
    {
        if (skill == null || skill.EffectParams == null) return false;
        if (!skill.EffectParams.ContainsKey("attack_type")) return false;
        string attackType = skill.EffectParams["attack_type"].AsString();
        return attackType.Contains("melee", System.StringComparison.OrdinalIgnoreCase)
            || attackType.Contains("strike", System.StringComparison.OrdinalIgnoreCase)
            || attackType.Contains("charge", System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMeleeElevationBlocked(Unit attacker, Unit defender, HexGrid? grid)
    {
        if (grid == null) return false;
        return IsMeleeElevationBlocked(attacker, defender, grid, attacker.GridPos);
    }

    public static bool IsMeleeElevationBlocked(Unit attacker, Unit defender, HexGrid? grid, Vector2I attackerPos)
    {
        if (grid == null || !IsMeleeWeaponAttack(attacker)) return false;
        var attackerCell = grid.GetCell(attackerPos.X, attackerPos.Y);
        var defenderCell = grid.GetCell(defender.GridPos.X, defender.GridPos.Y);
        return IsMeleeElevationBlocked(attacker, defender, attackerCell, defenderCell);
    }

    public static bool IsMeleeElevationBlocked(Unit attacker, Unit defender, HexCell? attackerCell, HexCell? defenderCell)
    {
        if (!IsMeleeWeaponAttack(attacker) || attackerCell == null || defenderCell == null) return false;
        return Mathf.Abs(attackerCell.Elevation - defenderCell.Elevation) >= MeleeElevationBlockThreshold;
    }

    public static bool IsMeleeSkillElevationBlocked(Unit attacker, string skillEffect, HexCell targetCell, HexGrid? grid)
    {
        if (!IsMeleeSkill(skillEffect) || targetCell.Occupant == null) return false;
        return grid != null && IsMeleeElevationBlocked(attacker, targetCell.Occupant, grid);
    }

    public static bool IsMeleeSkillElevationBlocked(Unit attacker, string skillEffect, HexCell targetCell, HexGrid? grid, Vector2I attackerPos)
    {
        if (!IsMeleeSkill(skillEffect) || targetCell.Occupant == null) return false;
        return grid != null && IsMeleeElevationBlocked(attacker, targetCell.Occupant, grid, attackerPos);
    }

    public static bool IsMeleeCareerElevationBlocked(Unit attacker, CareerSkillData? skill, HexCell targetCell, HexGrid? grid)
    {
        if (!IsMeleeCareerSkill(skill) || targetCell.Occupant == null) return false;
        return grid != null && IsMeleeElevationBlocked(attacker, targetCell.Occupant, grid);
    }

    public static (bool CanAttack, string Reason, int Distance, int Range, int ApCost) CanAttackBattleAnchor(
        Unit attacker,
        CombatManager.BattleAnchorState anchor)
    {
        if (attacker == null || !GodotObject.IsInstanceValid(attacker))
            return (false, "未选中动作单位。", 0, 0, 0);

        var weapon = attacker.GetMainHand() as WeaponData;
        int apCost = weapon?.ApCost ?? 4;
        int range = attacker.GetWeaponRange();
        int distance = HexUtils.AxialDistance(attacker.GridPos, anchor.Position);

        if (!anchor.Destructible)
            return (false, "战旗不可被攻击。", distance, range, apCost);
        if (distance > range)
            return (false, $"目标超出射程:战旗 (距离 {distance}, 射程 {range})", distance, range, apCost);
        if (attacker.CurrentAp < apCost)
            return (false, $"行动力不足:战旗 (需要 {apCost}, 当前 {attacker.CurrentAp:F0})", distance, range, apCost);

        return (true, string.Empty, distance, range, apCost);
    }
}
