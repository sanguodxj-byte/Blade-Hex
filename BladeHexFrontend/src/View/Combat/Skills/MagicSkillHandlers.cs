// MagicSkillHandlers.cs
// 法术主动技能 handler — INT 系
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.Skills;

/// <summary>法术主动技能执行器</summary>
public static class MagicSkillHandlers
{
    public static void ManaShield(in SkillHandlerContext ctx)
    {
        if (ctx.Attacker.Data!.CurrentMana < 5) { SkillUtils.Fail(ctx.Result, "魔力不足"); return; }
        ctx.Attacker.Data!.CurrentMana -= 5;
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", ctx.Attacker }, { "effect_id", "shield" }, { "duration", 3 }
        });
    }

    public static void TimeWarp(in SkillHandlerContext ctx)
    {
        if (ctx.Attacker.Data!.CurrentMana < 10) { SkillUtils.Fail(ctx.Result, "魔力不足"); return; }
        ctx.Attacker.Data!.CurrentMana -= 10;
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", ctx.Attacker }, { "effect_id", "haste" }, { "duration", 1 }
        });
    }

    public static void ArcaneBurst(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }
        int dmg = RPGRuleEngine.RollDice(2, 8);
        int intMod = RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Intel);
        if (ctx.Attacker.HasSkillEffect("knowledge_power")) dmg += intMod;
        target.TakeDamage(dmg);
        ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "damage" }, { "target", target }, { "value", dmg }, { "damage_type", "arcane" }
        });
    }

    public static void ManaDrain(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }
        int drained = RPGRuleEngine.RollDice(2, 6);
        if (target.Data != null) target.Data.CurrentMana = Math.Max(0, target.Data.CurrentMana - drained);
        if (ctx.Attacker.Data != null) ctx.Attacker.Data.CurrentMana += drained;
        ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "mana_drain" }, { "target", target }, { "value", drained }
        });
    }

    public static void ChainLightning(in SkillHandlerContext ctx)
    {
        var firstTarget = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (firstTarget == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }

        int intMod = RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Intel);
        var hitTargets = new List<Unit> { firstTarget };
        int dmg1 = RPGRuleEngine.RollDice(3, 6) + intMod;
        firstTarget.TakeDamage(dmg1);
        ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "damage" }, { "target", firstTarget }, { "value", dmg1 }, { "damage_type", "lightning" }
        });

        int jumps = 0;
        foreach (var enemy in ctx.Enemies)
        {
            if (jumps >= 2) break;
            if (hitTargets.Contains(enemy) || !GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0) continue;
            var lastHit = hitTargets[^1];
            int dist = HexUtils.Distance(lastHit.GridPos.X, lastHit.GridPos.Y, enemy.GridPos.X, enemy.GridPos.Y);
            if (dist <= 2)
            {
                int dmg = RPGRuleEngine.RollDice(2, 6) + intMod;
                enemy.TakeDamage(dmg);
                ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "type", "damage" }, { "target", enemy }, { "value", dmg }, { "damage_type", "lightning" }
                });
                hitTargets.Add(enemy);
                jumps++;
            }
        }
    }

    public static void ArcaneBomb(in SkillHandlerContext ctx)
    {
        int intMod = RPGRuleEngine.GetStatModifier(ctx.Attacker.Data!.Intel);
        int baseDmg = RPGRuleEngine.RollDice(3, 6) + intMod;
        var targets = new List<Vector2I> { ctx.TargetCell };
        targets.AddRange(HexUtils.GetNeighbors(ctx.TargetCell.X, ctx.TargetCell.Y));
        foreach (var pos in targets)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                int dmg = Math.Max(1, baseDmg / 2 + RPGRuleEngine.RollDice(1, 4));
                if (ctx.Attacker.HasSkillEffect("knowledge_power")) dmg += intMod;
                target.TakeDamage(dmg);
                ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "type", "damage" }, { "target", target }, { "value", dmg }, { "damage_type", "arcane" }
                });
            }
        }
    }

    public static void VoidGate(in SkillHandlerContext ctx)
    {
        var oldPos = ctx.Attacker.GridPos;
        ctx.Attacker.GridPos = ctx.TargetCell;
        ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "teleport" }, { "target", ctx.Attacker }, { "destination", ctx.TargetCell }, { "origin", oldPos }
        });
    }
}
