// AIBehaviorRegressionTests.cs
// 敌人 AI review 回归测试：策略接线、战术包夹动作、MoveThenAttack AP 预算。
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.AI.Tests;

public static class AIBehaviorRegressionTests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;
        foreach (var (name, run) in EnumerateTests())
        {
            try
            {
                var (ok, msg) = run();
                if (ok) { passed++; details.Add($"  [PASS] {name}"); }
                else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
            }
            catch (Exception ex)
            {
                failed++;
                details.Add($"  [FAIL] {name}: Exception {ex.GetType().Name}: {ex.Message}");
            }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, Func<(bool, string)> run)> EnumerateTests()
    {
        yield return (nameof(AllStrategies_HavePriorityMapping), AllStrategies_HavePriorityMapping);
        yield return (nameof(TacticalFlankAction_IsNotIdle), TacticalFlankAction_IsNotIdle);
        yield return (nameof(MoveThenAttack_ReservesAttackAp), MoveThenAttack_ReservesAttackAp);
    }

    private static (bool, string) AllStrategies_HavePriorityMapping()
    {
        foreach (UnitData.AIStrategy strategy in Enum.GetValues(typeof(UnitData.AIStrategy)))
        {
            int priority = AIController.GetStrategyPriority(strategy);
            if (priority >= 99)
                return (false, $"strategy {strategy} has no priority mapping");
        }
        return (true, "");
    }

    private static (bool, string) TacticalFlankAction_IsNotIdle()
    {
        var grid = MakeHexGrid(radius: 3);
        var attacker = MakeUnit("tactical", UnitData.AIStrategy.Tactical, ap: 10, weaponRange: 1, weaponAp: 4, isEnemy: true);
        var target = MakeUnit("target", UnitData.AIStrategy.Instinct, ap: 10, weaponRange: 1, weaponAp: 4, isEnemy: false);

        Place(grid, attacker, new Vector2I(0, 0));
        Place(grid, target, new Vector2I(2, 0));
        target.Facing = 3; // 让目标背后方向落在 (3,0)，攻击者可移动包夹。

        var controller = new AIController { DifficultyConfig = new AIDifficultyConfig { Difficulty = AIDifficultyConfig.DifficultyLevel.Normal } };
        controller.Initialize();

        var action = controller.DecideActionForUnit(attacker, new List<Unit> { target }, new List<Unit> { attacker }, grid);
        Cleanup(grid);

        if (action.Type == AIAction.ActionType.Idle)
            return (false, "tactical flank action should not be Idle");
        if (action.Type != AIAction.ActionType.MoveThenAttack && action.Type != AIAction.ActionType.Attack)
            return (false, $"expected Attack/MoveThenAttack, got {action.Type}");
        return (true, "");
    }

    private static (bool, string) MoveThenAttack_ReservesAttackAp()
    {
        var grid = MakeHexGrid(radius: 6);
        var attacker = MakeUnit("attacker", UnitData.AIStrategy.Instinct, ap: 6, weaponRange: 1, weaponAp: 4, isEnemy: true);
        var target = MakeUnit("target", UnitData.AIStrategy.Instinct, ap: 10, weaponRange: 1, weaponAp: 4, isEnemy: false);

        Place(grid, attacker, new Vector2I(0, 0));
        Place(grid, target, new Vector2I(4, 0));

        var controller = new AIController { DifficultyConfig = new AIDifficultyConfig { Difficulty = AIDifficultyConfig.DifficultyLevel.Legendary } };
        controller.Initialize();

        var action = controller.DecideActionForUnit(attacker, new List<Unit> { target }, new List<Unit> { attacker }, grid);
        var weapon = attacker.Model.GetMainHand() as WeaponData;
        int attackAp = weapon?.ApCost ?? 4;
        float moveCost = grid.GetPathCost(attacker.GridPos, action.MovePath);
        Cleanup(grid);

        if (action.Type == AIAction.ActionType.MoveThenAttack && moveCost + attackAp > attacker.CurrentAp)
            return (false, $"MoveThenAttack did not reserve AP: move={moveCost}, attack={attackAp}, ap={attacker.CurrentAp}");

        return (true, "");
    }

    private static HexGrid MakeHexGrid(int radius)
    {
        var grid = new HexGrid();
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Math.Max(-radius, -q - radius);
            int r2 = Math.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                var pos = new Vector2I(q, r);
                grid.Cells[pos] = new HexCell
                {
                    GridPos = pos,
                    Elevation = 1,
                    CoverType = 0,
                    Data = new BattleCellData
                    {
                        terrainType = BattleCellData.TerrainType.Plains,
                        isPassable = true,
                        moveCost = 1,
                    }
                };
            }
        }
        return grid;
    }

    private static Unit MakeUnit(string name, UnitData.AIStrategy strategy, float ap, int weaponRange, int weaponAp, bool isEnemy)
    {
        var data = new UnitData
        {
            UnitName = name,
            IsEnemy = isEnemy,
            aiStrategy = strategy,
            BaseMaxHp = 20,
            BaseAp = 10,
            BaseMoveRange = 6,
            Str = 10,
            Dex = 10,
            Con = 10,
            PrimaryMainHand = new WeaponData
            {
                ItemName = $"{name}_weapon",
                RangeCells = weaponRange,
                ApCost = weaponAp,
                DamageDiceCount = 1,
                DamageDiceSides = 4,
            }
        };

        var unit = new Unit
        {
            Name = name,
            Data = data,
            CurrentHp = 20,
            CurrentAp = ap,
            IsPlayerSide = !isEnemy,
        };
        return unit;
    }

    private static void Place(HexGrid grid, Unit unit, Vector2I pos)
    {
        unit.GridPos = pos;
        if (grid.Cells.TryGetValue(pos, out var cell))
            cell.Occupant = unit;
    }

    private static void Cleanup(HexGrid grid)
    {
        foreach (var cell in grid.Cells.Values)
        {
            if (cell.Occupant != null)
            {
                if (GodotObject.IsInstanceValid(cell.Occupant))
                    cell.Occupant.Free();
                cell.Occupant = null;
            }
            if (GodotObject.IsInstanceValid(cell))
                cell.Free();
        }
        if (GodotObject.IsInstanceValid(grid))
            grid.Free();
    }
}
