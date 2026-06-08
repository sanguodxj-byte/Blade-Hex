using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat;

/// <summary>
/// 消耗品管理器 — 处理药水使用、投掷物投掷、卷轴施放
/// </summary>
[GlobalClass]
public partial class ConsumableManager : Node
{
    [Signal] public delegate void ConsumableUsedEventHandler(Unit user, ConsumableData item, Godot.Collections.Dictionary result);

    public static Godot.Collections.Dictionary UseConsumable(Unit user, ConsumableData item, Vector2I targetCell = default, HexGrid? grid = null)
    {
        var result = new Godot.Collections.Dictionary { { "success", false }, { "effect", "" }, { "amount", 0 }, { "targets_affected", 0 } };

        switch (item.consumableType)
        {
            case ConsumableData.ConsumableType.HealingPotion:
            case ConsumableData.ConsumableType.StrongHealing:
                result = UseHealingPotion(user, item);
                break;
            case ConsumableData.ConsumableType.Antidote:
                result = UseAntidote(user, item);
                break;
            case ConsumableData.ConsumableType.FireOil:
                result = UseThrownItem(user, item, targetCell, grid, "fire");
                break;
            case ConsumableData.ConsumableType.HolyWater:
                result = UseThrownItem(user, item, targetCell, grid, "arcane");
                break;
            case ConsumableData.ConsumableType.SpellScroll:
                result = UseScroll(user, item, targetCell, grid);
                break;
        }

        if (result["success"].AsBool())
        {
            RemoveFromInventory(user, item);
        }

        return result;
    }

    private static Godot.Collections.Dictionary UseHealingPotion(Unit user, ConsumableData item)
    {
        // v0.7: 使用者技能盘 heal_amount 节点（如 con_b07 生命之环、wis 系治疗节点）累加。
        int heal = RPGRuleEngine.RollDice(item.HealDiceCount, item.HealDiceSides)
                 + item.HealBonus
                 + (user.SkillTree?.GetHealBonus() ?? 0);
        int actual = user.Heal(heal);
        return new Godot.Collections.Dictionary { { "success", true }, { "effect", "heal" }, { "amount", actual }, { "targets_affected", 1 } };
    }

    private static Godot.Collections.Dictionary UseAntidote(Unit user, ConsumableData item)
    {
        return new Godot.Collections.Dictionary
        {
            { "success", true },
            { "effect", "cure_poison" },
            { "amount", 0 },
            { "targets_affected", 1 },
            { "remove_effects", new Godot.Collections.Array { "poison" } }
        };
    }

    private static Godot.Collections.Dictionary UseThrownItem(Unit user, ConsumableData item, Vector2I targetCell, HexGrid? grid, string damageType)
    {
        if (grid == null) return new Godot.Collections.Dictionary { { "success", false }, { "effect", "no_grid" }, { "amount", 0 }, { "targets_affected", 0 } };

        int affected = 0;
        int totalDamage = 0;

        var targetCells = new List<Vector2I> { targetCell };
        if (item.AoeRadius > 0)
        {
            targetCells.AddRange(grid.GetCellsInRange(targetCell.X, targetCell.Y, item.AoeRadius));
        }

        foreach (var pos in targetCells.Distinct())
        {
            var cell = grid.GetCell(pos.X, pos.Y);
            if (cell?.Occupant is not Unit target) continue;

            if (damageType == "arcane" && target.Data?.IsEnemy == true && target.Data.enemyType != UnitData.EnemyType.Undead) continue;

            int dmg = RPGRuleEngine.RollDice(item.DamageDiceCount, item.DamageDiceSides);
            target.TakeDamage(dmg);
            totalDamage += dmg;
            affected++;
        }

        return new Godot.Collections.Dictionary { { "success", true }, { "effect", $"{damageType}_damage" }, { "amount", totalDamage }, { "targets_affected", affected } };
    }

    private static Godot.Collections.Dictionary UseScroll(Unit user, ConsumableData item, Vector2I targetCell, HexGrid? grid)
    {
        if (string.IsNullOrEmpty(item.LinkedSpellId))
            return new Godot.Collections.Dictionary { { "success", false }, { "effect", "no_spell" }, { "amount", 0 }, { "targets_affected", 0 } };

        int dmg = item.DamageDiceCount > 0 ? RPGRuleEngine.RollDice(item.DamageDiceCount, item.DamageDiceSides) : 0;

        return new Godot.Collections.Dictionary
        {
            { "success", true },
            { "effect", "scroll_cast" },
            { "amount", dmg },
            { "targets_affected", 1 }
        };
    }

    private static void RemoveFromInventory(Unit user, ConsumableData item)
    {
        user.Data?.Consumables.Remove(item);
    }
}
