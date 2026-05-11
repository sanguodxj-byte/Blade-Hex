using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat;

/// <summary>
/// 环境事件系统 — 战场环境效果（暴风雨、浓雾、地震等）
/// </summary>
public partial class EnvironmentEventSystem : Node
{
    public enum EnvironmentEventType { Storm, Fog, Earthquake, PoisonFog, HolyLight, Lava }

    private static readonly Dictionary<EnvironmentEventType, int> EventIntervals = new()
    {
        { EnvironmentEventType.Storm, 3 },
        { EnvironmentEventType.Fog, 1 },
        { EnvironmentEventType.Earthquake, 5 },
        { EnvironmentEventType.PoisonFog, 2 },
        { EnvironmentEventType.HolyLight, 1 },
        { EnvironmentEventType.Lava, 3 },
    };

    public List<EnvironmentEventType> ActiveEvents { get; private set; } = new();

    public StatusEffectManager? StatusEffectManagerRef { get; set; }

    public void ProcessEvents(int roundNumber, HexGrid grid, IEnumerable<Unit> allUnits)
    {
        foreach (var eventType in ActiveEvents)
        {
            int interval = EventIntervals.GetValueOrDefault(eventType, 1);
            if (roundNumber > 0 && roundNumber % interval != 0) continue;

            switch (eventType)
            {
                case EnvironmentEventType.Storm:
                    ProcessStorm(grid, allUnits);
                    break;
                case EnvironmentEventType.Fog:
                    // 标记效果，在视野计算中处理
                    break;
                case EnvironmentEventType.Earthquake:
                    ProcessEarthquake(grid);
                    break;
                case EnvironmentEventType.PoisonFog:
                    ProcessPoisonFog(grid, allUnits);
                    break;
                case EnvironmentEventType.HolyLight:
                    ProcessHolyLight(allUnits);
                    break;
                case EnvironmentEventType.Lava:
                    ProcessLava(grid, allUnits);
                    break;
            }
        }
    }

    public void ActivateEvent(EnvironmentEventType eventType)
    {
        if (!ActiveEvents.Contains(eventType)) ActiveEvents.Add(eventType);
    }

    public void DeactivateEvent(EnvironmentEventType eventType)
    {
        ActiveEvents.Remove(eventType);
    }

    private void ProcessStorm(HexGrid grid, IEnumerable<Unit> allUnits)
    {
        // 暴风雨：所有远程攻击射程-2，所有暴露单位受到1d4闪电伤害
        foreach (var unit in allUnits)
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.CurrentHp <= 0) continue;
            var cell = grid.GetCell(unit.GridPos.X, unit.GridPos.Y);
            // 只在室外（非森林/密林/废墟/墙壁格）受到伤害
            if (cell?.Data != null &&
                cell.Data.terrainType != BattleCellData.TerrainType.Forest &&
                cell.Data.terrainType != BattleCellData.TerrainType.DenseForest &&
                cell.Data.terrainType != BattleCellData.TerrainType.Ruins &&
                cell.Data.terrainType != BattleCellData.TerrainType.Wall)
            {
                int dmg = RPGRuleEngine.RollDice(1, 4);
                unit.TakeDamage(dmg);
            }
        }
    }

    private void ProcessEarthquake(HexGrid grid)
    {
        var rand = new Random();
        int destroyCount = rand.Next(2, 4);
        var cells = grid.GetCells().ToList();
        if (cells.Count == 0) return;

        for (int i = 0; i < destroyCount; i++)
        {
            var cell = cells[rand.Next(cells.Count)];
            // 将建筑废墟变为平地，墙壁变为废墟
            if (cell.Data != null)
            {
                if (cell.Data.terrainType == BattleCellData.TerrainType.Wall)
                    cell.Data.terrainType = BattleCellData.TerrainType.Ruins;
                else if (cell.Data.terrainType == BattleCellData.TerrainType.Ruins)
                    cell.Data.terrainType = BattleCellData.TerrainType.Plains;
            }
        }
    }

    private void ProcessPoisonFog(HexGrid grid, IEnumerable<Unit> allUnits)
    {
        if (StatusEffectManagerRef == null) return;
        foreach (var unit in allUnits)
        {
            if (!GodotObject.IsInstanceValid(unit) || unit.CurrentHp <= 0) continue;
            var cell = grid.GetCell(unit.GridPos.X, unit.GridPos.Y);
            if (cell != null && cell.Elevation <= 1)
            {
                StatusEffectManagerRef.ApplyEffect(unit, "poison", 2);
            }
        }
    }

    private void ProcessHolyLight(IEnumerable<Unit> allUnits)
    {
        foreach (var unit in allUnits.Where(u => u.Data != null && !u.Data.IsEnemy))
        {
            if (unit.Data!.KnownSpells.Any(s => s.HealDiceCount > 0))
            {
                int heal = RPGRuleEngine.RollDice(1, 4);
                unit.CurrentHp = Math.Min(unit.CurrentHp + heal, unit.GetMaxHp());
            }
        }
    }

    private void ProcessLava(HexGrid grid, IEnumerable<Unit> allUnits)
    {
        var rand = new Random();
        var cells = grid.GetCells().ToList();
        if (cells.Count == 0) return;

        for (int i = 0; i < rand.Next(1, 3); i++)
        {
            var cell = cells[rand.Next(cells.Count)];
            if (cell.Occupant is Unit unit)
            {
                unit.TakeDamage(RPGRuleEngine.RollDice(2, 6));
            }
        }
    }
}
