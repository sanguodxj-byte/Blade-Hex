using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat;

/// <summary>
/// 技能特效执行引擎 — 静态工具类
/// 负责主动技能的执行逻辑
/// </summary>
public static class SkillEffectExecutor
{
    // ============================================================================
    // 基础接口
    // ============================================================================

    public static Godot.Collections.Dictionary GetSkillConfig(string skillEffect) => SkillRegistry.GetSkillConfig(skillEffect);
    public static bool IsActiveSkill(string skillEffect) => SkillRegistry.IsActiveSkill(skillEffect);
    public static bool IsPassiveSkill(string skillEffect) => SkillRegistry.IsPassiveSkill(skillEffect);

    // ============================================================================
    // 主动技能执行 — 主入口
    // ============================================================================

    public static Godot.Collections.Dictionary ExecuteActiveSkill(
        Unit attacker,
        string skillEffect,
        Vector2I targetCell,
        HexGrid? grid,
        IEnumerable<Unit> allUnits,
        IEnumerable<Unit> playerUnits,
        IEnumerable<Unit> enemyUnits
    )
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.Count == 0 || !IsActiveSkill(skillEffect))
        {
            return new Godot.Collections.Dictionary {
                { "success", false },
                { "reason", "未知或非主动技能" }
            };
        }

        var allies = !attacker.Data!.IsEnemy ? playerUnits : enemyUnits;
        var enemies = !attacker.Data!.IsEnemy ? enemyUnits : playerUnits;

        var result = new Godot.Collections.Dictionary
        {
            { "success", true },
            { "results", new Godot.Collections.Array() },
            { "action_cost", cfg.ContainsKey("action_cost") ? cfg["action_cost"].AsString() : "major" },
            { "vfx_type", cfg.ContainsKey("vfx") ? cfg["vfx"].AsString() : "" },
            { "status_effects", new Godot.Collections.Array() }
        };

        switch (skillEffect)
        {
            case "double_attack":
                ExecDoubleAttack(attacker, targetCell, grid, enemies, result);
                break;
            case "whirlwind":
                ExecWhirlwind(attacker, grid, enemies, result);
                break;
            case "battle_cry":
                ExecBattleCry(attacker, grid, enemies, allies, result);
                break;
            case "blood_vortex":
                ExecBloodVortex(attacker, grid, enemies, result);
                break;
            case "aimed_shot":
                ExecAimedShot(attacker, targetCell, grid, enemies, result);
                break;
            case "double_shot":
                ExecDoubleShot(attacker, targetCell, grid, enemies, result);
                break;
            case "scatter_shot":
                ExecScatterShot(attacker, targetCell, grid, enemies, result);
                break;
            case "stealth":
                ExecStealth(attacker, result);
                break;
            case "shadow_clone":
                ExecShadowClone(attacker, result);
                break;
            case "trick_arrow":
                ExecTrickArrow(attacker, targetCell, grid, enemies, result);
                break;
            case "poison_blade":
                ExecPoisonBlade(attacker, targetCell, grid, enemies, result);
                break;
            case "shield_bash":
                ExecShieldBash(attacker, targetCell, grid, enemies, result);
                break;
            case "taunt":
                ExecTaunt(attacker, grid, enemies, result);
                break;
            case "unyielding_bulwark":
                ExecUnyieldingBulwark(attacker, result);
                break;
            case "field_medic":
                ExecFieldMedic(attacker, targetCell, grid, allies, result);
                break;
            case "basic_heal":
                ExecBasicHeal(attacker, targetCell, grid, allies, result);
                break;
            case "mana_shield":
                ExecManaShield(attacker, result);
                break;
            case "time_warp":
                ExecTimeWarp(attacker, result);
                break;
            case "blessing":
                ExecBlessing(attacker, targetCell, grid, allies, result);
                break;
            case "war_cry":
                ExecWarCry(attacker, grid, allies, result);
                break;
            case "inspire":
                ExecInspire(attacker, allies, result);
                break;
            default:
                result["success"] = false;
                result["reason"] = $"技能 {skillEffect} 逻辑尚未迁移";
                break;
        }

        return result;
    }

    // ============================================================================
    // 具体的技能执行逻辑
    // ============================================================================

    private static void ExecDoubleAttack(Unit attacker, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null)
        {
            result["success"] = false;
            result["reason"] = "目标格没有敌人";
            return;
        }

        // 第一次攻击
        var r1 = CombatResolver.ResolveAttack(attacker, target, grid, false);
        result["results"].AsGodotArray().Add(r1);

        // 第二次攻击 (命中-3)
        var r2 = CombatResolver.ResolveAttack(attacker, target, grid, false, false, -3);
        result["results"].AsGodotArray().Add(r2);
    }

    private static void ExecWhirlwind(Unit attacker, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        if (grid == null) return;
        var neighbors = HexUtils.GetNeighbors(attacker.GridPos.X, attacker.GridPos.Y);
        foreach (var pos in neighbors)
        {
            var target = FindUnitAt(pos, enemies);
            if (target != null)
            {
                var r = CombatResolver.ResolveAttack(attacker, target, grid, false);
                result["results"].AsGodotArray().Add(r);
            }
        }
    }

    private static void ExecBasicHeal(Unit attacker, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> allies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, allies);
        if (target == null)
        {
            result["success"] = false;
            result["reason"] = "目标格没有盟友";
            return;
        }

        int wisMod = RPGRuleEngine.GetStatModifier(attacker.Data!.Wis);
        int heal = RPGRuleEngine.RollDice(1, 8) + wisMod;
        target.CurrentHp = Math.Min(target.CurrentHp + heal, target.GetMaxHp());

        result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "heal" },
            { "target", target },
            { "value", heal }
        });
    }

    private static void ExecStealth(Unit attacker, Godot.Collections.Dictionary result)
    {
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", attacker },
            { "effect_id", "invisibility" },
            { "duration", 99 }
        });
    }

    // ============================================================================
    // 辅助方法
    // ============================================================================

    private static Unit? FindUnitAt(Vector2I pos, IEnumerable<Unit> units)
    {
        return units.FirstOrDefault(u => u.GridPos == pos);
    }

    // --- STR 主动技能 ---

    private static void ExecBattleCry(Unit attacker, HexGrid? grid, IEnumerable<Unit> enemies, IEnumerable<Unit> allies, Godot.Collections.Dictionary result)
    {
        // 震慑周围敌人下回合攻击-2，友军士气+3
        var neighbors = grid != null ? HexUtils.GetNeighbors(attacker.GridPos.X, attacker.GridPos.Y) : Array.Empty<Vector2I>();
        foreach (var pos in neighbors)
        {
            var enemy = FindUnitAt(pos, enemies);
            if (enemy != null)
            {
                result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "target", enemy },
                    { "effect_id", "fear" },
                    { "duration", 2 },
                    { "stat_modifiers", new Godot.Collections.Dictionary { { "attack_bonus", -2 } } }
                });
            }
        }
        foreach (var ally in allies)
        {
            if (GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
                MoraleSystem.ChangeMorale(ally, 3);
        }
    }

    private static void ExecBloodVortex(Unit attacker, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        if (grid == null) return;
        var neighbors = HexUtils.GetNeighbors(attacker.GridPos.X, attacker.GridPos.Y);
        int hits = 0;
        foreach (var pos in neighbors)
        {
            var target = FindUnitAt(pos, enemies);
            if (target != null)
            {
                var r = CombatResolver.ResolveAttack(attacker, target, grid, false);
                result["results"].AsGodotArray().Add(r);
                if (r.ContainsKey("hit") && r["hit"].AsBool())
                {
                    hits++;
                    int heal = RPGRuleEngine.RollDice(1, 6);
                    attacker.CurrentHp = Math.Min(attacker.CurrentHp + heal, attacker.GetMaxHp());
                }
            }
        }
    }

    // --- DEX 主动技能 ---

    private static void ExecAimedShot(Unit attacker, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null)
        {
            result["success"] = false;
            result["reason"] = "目标格没有敌人";
            return;
        }
        // 瞄准后射击优势 + 伤害x2
        var r = CombatResolver.ResolveAttack(attacker, target, grid, true);
        if (r.ContainsKey("hit") && r["hit"].AsBool() && r.ContainsKey("damage"))
        {
            r["damage"] = r["damage"].AsInt32() * 2;
        }
        result["results"].AsGodotArray().Add(r);
    }

    private static void ExecDoubleShot(Unit attacker, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        // 射击2次各-2命中
        var target = FindUnitAt(targetCell, enemies);
        if (target == null)
        {
            result["success"] = false;
            result["reason"] = "目标格没有敌人";
            return;
        }
        var r1 = CombatResolver.ResolveAttack(attacker, target, grid, false, false, -2);
        result["results"].AsGodotArray().Add(r1);
        var r2 = CombatResolver.ResolveAttack(attacker, target, grid, false, false, -2);
        result["results"].AsGodotArray().Add(r2);
    }

    private static void ExecScatterShot(Unit attacker, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        // 锥形范围：目标格及其相邻格
        if (grid == null) return;
        var targets = new List<Vector2I> { targetCell };
        targets.AddRange(HexUtils.GetNeighbors(targetCell.X, targetCell.Y));
        foreach (var pos in targets)
        {
            var target = FindUnitAt(pos, enemies);
            if (target != null)
            {
                var r = CombatResolver.ResolveAttack(attacker, target, grid, false, false, -2);
                result["results"].AsGodotArray().Add(r);
            }
        }
    }

    private static void ExecShadowClone(Unit attacker, Godot.Collections.Dictionary result)
    {
        // 位移+残影：获得幻影效果（自动闪避1次）
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", attacker },
            { "effect_id", "phantom" },
            { "duration", 3 }
        });
    }

    private static void ExecTrickArrow(Unit attacker, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null)
        {
            result["success"] = false;
            result["reason"] = "目标格没有敌人";
            return;
        }
        // 1d10伤害 + 随机debuff
        int dmg = RPGRuleEngine.RollDice(1, 10);
        target.TakeDamage(dmg);
        result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "damage" },
            { "target", target },
            { "value", dmg }
        });
        // 随机debuff: 失明/倒地(stun)/震慑(fear)
        string[] debuffs = { "blind", "stun", "fear" };
        string chosenDebuff = debuffs[new Random().Next(debuffs.Length)];
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target },
            { "effect_id", chosenDebuff },
            { "duration", 1 }
        });
    }

    private static void ExecPoisonBlade(Unit attacker, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null)
        {
            result["success"] = false;
            result["reason"] = "目标格没有敌人";
            return;
        }
        var r = CombatResolver.ResolveAttack(attacker, target, grid, false);
        result["results"].AsGodotArray().Add(r);
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target },
            { "effect_id", "poison" },
            { "duration", 3 }
        });
    }

    // --- CON 主动技能 ---

    private static void ExecShieldBash(Unit attacker, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, enemies);
        if (target == null)
        {
            result["success"] = false;
            result["reason"] = "目标格没有敌人";
            return;
        }
        var r = CombatResolver.ResolveAttack(attacker, target, grid, false);
        result["results"].AsGodotArray().Add(r);
        // 推开目标1格
        if (r.ContainsKey("hit") && r["hit"].AsBool() && grid != null)
        {
            var pushDir = FacingSystem.GetAttackDirection(attacker.GridPos, target);
            int facing = attacker.Data!.Facing;
            var pushTarget = HexUtils.GetNeighbor(target.GridPos.X, target.GridPos.Y, facing);
            var pushCell = grid.GetCell(pushTarget.X, pushTarget.Y);
            if (pushCell != null && pushCell.Occupant == null)
            {
                target.GridPos = pushTarget;
            }
        }
    }

    private static void ExecTaunt(Unit attacker, HexGrid? grid, IEnumerable<Unit> enemies, Godot.Collections.Dictionary result)
    {
        if (grid == null) return;
        var neighbors = HexUtils.GetNeighbors(attacker.GridPos.X, attacker.GridPos.Y);
        foreach (var pos in neighbors)
        {
            var enemy = FindUnitAt(pos, enemies);
            if (enemy != null)
            {
                result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "target", enemy },
                    { "effect_id", "charmed" },
                    { "duration", 2 },
                    { "stat_modifiers", new Godot.Collections.Dictionary { { "forced_target_id", (long)attacker.GetInstanceId() } } }
                });
            }
        }
    }

    private static void ExecUnyieldingBulwark(Unit attacker, Godot.Collections.Dictionary result)
    {
        // 受伤减半 + 临时HP
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", attacker },
            { "effect_id", "shield" },
            { "duration", 2 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "damage_reduction_percent", 0.5 } } }
        });
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", attacker },
            { "effect_id", "temp_hp" },
            { "duration", 2 },
            { "stat_modifiers", new Godot.Collections.Dictionary { { "temp_hp_amount", RPGRuleEngine.RollDice(2, 6) } } }
        });
    }

    private static void ExecFieldMedic(Unit attacker, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> allies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, allies);
        if (target == null)
        {
            result["success"] = false;
            result["reason"] = "目标格没有盟友";
            return;
        }
        int wisMod = RPGRuleEngine.GetStatModifier(attacker.Data!.Wis);
        int heal = RPGRuleEngine.RollDice(2, 8) + wisMod;
        target.CurrentHp = Math.Min(target.CurrentHp + heal, target.GetMaxHp());
        result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "heal" },
            { "target", target },
            { "value", heal }
        });
        // 解除流血/中毒
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target },
            { "special", "remove_effects" },
            { "remove_ids", new Godot.Collections.Array { "bleed", "poison" } }
        });
    }

    // --- INT 主动技能 ---

    private static void ExecManaShield(Unit attacker, Godot.Collections.Dictionary result)
    {
        // 消耗5魔力获得护盾
        if (attacker.Data!.CurrentMana < 5)
        {
            result["success"] = false;
            result["reason"] = "魔力不足";
            return;
        }
        attacker.Data!.CurrentMana -= 5;
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", attacker },
            { "effect_id", "shield" },
            { "duration", 3 }
        });
    }

    private static void ExecTimeWarp(Unit attacker, Godot.Collections.Dictionary result)
    {
        // 消耗10魔力获得额外次要行动
        if (attacker.Data!.CurrentMana < 10)
        {
            result["success"] = false;
            result["reason"] = "魔力不足";
            return;
        }
        attacker.Data!.CurrentMana -= 10;
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", attacker },
            { "effect_id", "haste" },
            { "duration", 1 }
        });
    }

    // --- WIS 主动技能 ---

    private static void ExecBlessing(Unit attacker, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> allies, Godot.Collections.Dictionary result)
    {
        var target = FindUnitAt(targetCell, allies);
        if (target == null)
        {
            result["success"] = false;
            result["reason"] = "目标格没有盟友";
            return;
        }
        result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target },
            { "effect_id", "bless" },
            { "duration", 3 }
        });
    }

    // --- CHA 主动技能 ---

    private static void ExecWarCry(Unit attacker, HexGrid? grid, IEnumerable<Unit> allies, Godot.Collections.Dictionary result)
    {
        if (grid == null) return;
        var neighbors = HexUtils.GetNeighbors(attacker.GridPos.X, attacker.GridPos.Y);
        foreach (var pos in neighbors)
        {
            var ally = FindUnitAt(pos, allies);
            if (ally != null && GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
            {
                result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "target", ally },
                    { "effect_id", "bless" },
                    { "duration", 2 },
                    { "stat_modifiers", new Godot.Collections.Dictionary { { "attack_bonus", 1 } } }
                });
            }
        }
    }

    private static void ExecInspire(Unit attacker, IEnumerable<Unit> allies, Godot.Collections.Dictionary result)
    {
        foreach (var ally in allies)
        {
            if (GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
                MoraleSystem.ChangeMorale(ally, 2);
        }
    }

    public static bool HasQuickCast(Unit unit) => PassiveSkillResolver.HasQuickCast(unit);
}
