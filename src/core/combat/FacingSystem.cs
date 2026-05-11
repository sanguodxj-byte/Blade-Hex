// FacingSystem.cs
// 朝向系统 + 控制区 + 借机攻击 + 包夹判定
// 对应策划案 03-战术战斗系统 → 四、控制区与借机攻击 / 五、包夹与伏击
// 迁移自 GDScript FacingSystem.gd
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat;

/// <summary>
/// 朝向系统 — 静态工具类
/// 负责朝向判定、控制区(ZOC)、包夹判定、冲锋/反击加成
/// </summary>
public static class FacingSystem
{
    // ========================================
    // 攻击方向枚举
    // ========================================

    public enum AttackDirection
    {
        Front,  // 正面 — 非包夹
        Flank,  // 侧翼 — 包夹
        Rear,   // 背后 — 背刺
    }

    // ========================================
    // 朝向系统 (Facing)
    // ========================================

    /// <summary>获取单位正面3格的坐标</summary>
    public static Vector2I[] GetFrontCells(Vector2I unitPos, int facing)
    {
        var cells = new Vector2I[3];
        for (int i = 0; i < 3; i++)
        {
            int offset = i - 1; // -1, 0, 1
            int dir = (facing + offset) % 6;
            if (dir < 0) dir += 6;
            cells[i] = HexUtils.GetNeighbor(unitPos.X, unitPos.Y, dir);
        }
        return cells;
    }

    /// <summary>获取单位侧翼2格的坐标</summary>
    public static Vector2I[] GetFlankCells(Vector2I unitPos, int facing)
    {
        var cells = new Vector2I[2];
        int[] offsets = [2, -2];
        for (int i = 0; i < 2; i++)
        {
            int dir = (facing + offsets[i]) % 6;
            if (dir < 0) dir += 6;
            cells[i] = HexUtils.GetNeighbor(unitPos.X, unitPos.Y, dir);
        }
        return cells;
    }

    /// <summary>获取单位背后1格的坐标</summary>
    public static Vector2I GetRearCell(Vector2I unitPos, int facing)
    {
        int rearDir = (facing + 3) % 6;
        return HexUtils.GetNeighbor(unitPos.X, unitPos.Y, rearDir);
    }

    /// <summary>判定攻击方向（攻击者相对于目标的位置）</summary>
    public static AttackDirection GetAttackDirection(Vector2I attackerPos, Unit target)
    {
        var flankCells = GetFlankCells(target.GridPos, target.Data!.Facing);
        var rearCell = GetRearCell(target.GridPos, target.Data!.Facing);

        if (attackerPos == rearCell)
            return AttackDirection.Rear;
        foreach (var fc in flankCells)
            if (attackerPos == fc)
                return AttackDirection.Flank;
        return AttackDirection.Front;
    }

    // ========================================
    // 控制区 (Zone of Control)
    // ========================================

    /// <summary>获取单位投射控制区的格子列表</summary>
    public static Vector2I[] GetZocCells(Unit unit)
    {
        if (!HasZoc(unit))
            return [];

        if (unit.Data!.IsDefending)
        {
            var cells = new Vector2I[6];
            for (int dir = 0; dir < 6; dir++)
                cells[dir] = HexUtils.GetNeighbor(unit.GridPos.X, unit.GridPos.Y, dir);
            return cells;
        }
        return GetFrontCells(unit.GridPos, unit.Data!.Facing);
    }

    /// <summary>单位是否有控制区</summary>
    public static bool HasZoc(Unit unit)
    {
        var weapon = unit.GetMainHand();
        if (weapon is WeaponData w && w.IsRanged)
            return false;
        return true;
    }

    /// <summary>检查某个格子是否在任何敌方近战单位的控制区内</summary>
    public static bool IsInEnemyZoc(Vector2I pos, Unit[] enemyUnits)
    {
        foreach (var enemy in enemyUnits)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0)
                continue;
            if (!HasZoc(enemy))
                continue;
            var zoc = GetZocCells(enemy);
            foreach (var cell in zoc)
                if (cell == pos) return true;
        }
        return false;
    }

    /// <summary>检查单位是否在被敌方控制区内移动离开</summary>
    public static bool IsLeavingEnemyZoc(Vector2I from, Vector2I to, Unit[] enemyUnits)
    {
        foreach (var enemy in enemyUnits)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0)
                continue;
            if (!HasZoc(enemy))
                continue;
            var zoc = GetZocCells(enemy);
            bool fromInZoc = false;
            bool toInZoc = false;
            foreach (var cell in zoc)
            {
                if (cell == from) fromInZoc = true;
                if (cell == to) toInZoc = true;
            }
            if (fromInZoc && !toInZoc) return true;
        }
        return false;
    }

    // ========================================
    // 借机攻击 (Attack of Opportunity)
    // ========================================

    /// <summary>检查是否触发借机攻击，返回触发AoO的敌方单位（或null）</summary>
    public static Unit? ShouldTriggerAoo(Unit mover, Vector2I from, Vector2I to, Unit[] enemyUnits)
    {
        foreach (var enemy in enemyUnits)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0)
                continue;
            if (enemy.Data!.AooUsedThisTurn)
                continue;
            if (!HasZoc(enemy))
                continue;
            var zoc = GetZocCells(enemy);
            bool fromInZoc = false;
            bool toInZoc = false;
            foreach (var cell in zoc)
            {
                if (cell == from) fromInZoc = true;
                if (cell == to) toInZoc = true;
            }
            if (fromInZoc && !toInZoc)
                return enemy;
        }
        return null;
    }

    /// <summary>检查远程攻击是否在敌方近战ZoC内（被贴身）</summary>
    public static bool IsRangedInMeleeZoc(Unit unit, Unit[] enemyUnits)
    {
        foreach (var enemy in enemyUnits)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0)
                continue;
            if (!HasZoc(enemy))
                continue;
            var zoc = GetZocCells(enemy);
            foreach (var cell in zoc)
                if (cell == unit.GridPos) return true;
        }
        return false;
    }

    // ========================================
    // 包夹判定
    // ========================================

    /// <summary>
    /// 获取包夹加成
    /// 防御模式下单位免疫包夹
    /// </summary>
    public static FlankBonus GetFlankingBonus(Vector2I attackerPos, Unit defender)
    {
        if (defender.Data!.IsDefending)
            return new FlankBonus { DamageMultiplier = 1.0f, MoraleChange = 0, CanCounter = true };

        var direction = GetAttackDirection(attackerPos, defender);
        return direction switch
        {
            AttackDirection.Rear => new FlankBonus
            {
                DamageMultiplier = 1.5f,
                MoraleChange = -5,
                CanCounter = false,
            },
            AttackDirection.Flank => new FlankBonus
            {
                DamageMultiplier = 1.25f,
                MoraleChange = -3,
                CanCounter = true,
            },
            _ => new FlankBonus
            {
                DamageMultiplier = 1.0f,
                MoraleChange = 0,
                CanCounter = true,
            },
        };
    }

    /// <summary>获取多人夹击加成 — 目标周围不同方向的友方数量</summary>
    public static SurroundBonus GetSurroundingBonus(Unit target, Unit[] attackerAllies)
    {
        var occupiedDirs = new HashSet<int>();
        foreach (var ally in attackerAllies)
        {
            if (!GodotObject.IsInstanceValid(ally) || ally.CurrentHp <= 0)
                continue;
            int dist = HexUtils.Distance(ally.GridPos.X, ally.GridPos.Y, target.GridPos.X, target.GridPos.Y);
            if (dist == 1)
            {
                for (int dir = 0; dir < 6; dir++)
                {
                    if (HexUtils.GetNeighbor(target.GridPos.X, target.GridPos.Y, dir) == ally.GridPos)
                    {
                        occupiedDirs.Add(dir);
                        break;
                    }
                }
            }
        }

        int count = occupiedDirs.Count;
        if (count >= 4)
            return new SurroundBonus { HitBonus = 3, AcReduction = 2, DamageBonus = 0.1f };
        if (count >= 3)
            return new SurroundBonus { HitBonus = 2, AcReduction = 1, DamageBonus = 0.0f };
        if (count >= 2)
            return new SurroundBonus { HitBonus = 1, AcReduction = 0, DamageBonus = 0.0f };
        return new SurroundBonus { HitBonus = 0, AcReduction = 0, DamageBonus = 0.0f };
    }

    // ========================================
    // 冲锋检测 (Charge Detection)
    // ========================================

    /// <summary>检查移动路径是否构成冲锋（移动3格以上后发起近战攻击）</summary>
    public static bool IsCharge(Vector2I[] movePath) => movePath.Length >= 3;

    /// <summary>获取冲锋伤害加成</summary>
    public static ChargeBonus GetChargeBonus(Unit unit, bool isChargeMove)
    {
        if (!isChargeMove)
            return new ChargeBonus { DamageMultiplier = 1.0f, HasAdvantage = false };

        float baseMult = 1.25f;
        if (unit.Data!.IsMounted && unit.Data!.Mount != null)
            baseMult += unit.Data!.Mount.ChargeDamageBonus;

        return new ChargeBonus { DamageMultiplier = baseMult, HasAdvantage = true };
    }

    /// <summary>检查冲锋是否有效（不能在沙地/沼泽冲锋，骑乘不能在密林冲锋）</summary>
    public static bool CanCharge(Unit unit, HexGrid grid, Vector2I[] path)
    {
        foreach (var cellPos in path)
        {
            var cell = grid.GetCell(cellPos.X, cellPos.Y);
            if (cell?.Data == null)
                continue;
            // 沙地/沼泽不可冲锋
            if (cell.Data.terrainType == BattleCellData.TerrainType.Sand ||
                cell.Data.terrainType == BattleCellData.TerrainType.Swamp)
                return false;
            // 骑乘不可在密林/山地冲锋
            if (unit.Data!.IsMounted)
            {
                if (cell.Data.terrainType == BattleCellData.TerrainType.DenseForest ||
                    cell.Data.terrainType == BattleCellData.TerrainType.Mountain)
                    return false;
            }
        }
        return true;
    }

    // ========================================
    // 反击 (Retaliation)
    // ========================================

    /// <summary>获取反击伤害倍率</summary>
    public static float GetCounterAttackMultiplier(Unit defender, Vector2I attackerPos)
    {
        var flank = GetFlankingBonus(attackerPos, defender);
        if (!flank.CanCounter)
            return 0.0f;
        if (defender.Data!.CounterUsedThisTurn)
            return 0.0f;
        if (defender.Data!.IsDefending)
            return 1.0f;
        return 0.5f;
    }

    // ========================================
    // 结果类型
    // ========================================

    public struct FlankBonus
    {
        public float DamageMultiplier;
        public int MoraleChange;
        public bool CanCounter;
    }

    public struct ChargeBonus
    {
        public float DamageMultiplier;
        public int AttackBonus;
        public bool HasMoved;
        public bool HasAdvantage;
    }

    public struct SurroundBonus
    {
        public int HitBonus;
        public int AcReduction;
        public float DamageBonus;
    }
}