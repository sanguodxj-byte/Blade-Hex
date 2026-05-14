// RangedSkillHandlers.cs
// 远程主动技能 handler — DEX 射击系
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.Skills;

/// <summary>远程主动技能执行器</summary>
public static class RangedSkillHandlers
{
    public static void AimedShot(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }
        var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, 0, 2.0f);
        ctx.Result["results"].AsGodotArray().Add(r);
    }

    public static void DoubleShot(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }
        var r1 = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, -2);
        ctx.Result["results"].AsGodotArray().Add(r1);
        var r2 = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, -2);
        ctx.Result["results"].AsGodotArray().Add(r2);
    }

    public static void ScatterShot(in SkillHandlerContext ctx)
    {
        if (ctx.Grid == null) return;
        var targets = new List<Vector2I> { ctx.TargetCell };
        targets.AddRange(HexUtils.GetNeighbors(ctx.TargetCell.X, ctx.TargetCell.Y));
        foreach (var pos in targets)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, -2);
                ctx.Result["results"].AsGodotArray().Add(r);
            }
        }
    }

    public static void MultiShot(in SkillHandlerContext ctx)
    {
        var targets = new List<Vector2I> { ctx.TargetCell };
        targets.AddRange(HexUtils.GetNeighbors(ctx.TargetCell.X, ctx.TargetCell.Y));
        int shotCount = 0;
        foreach (var pos in targets)
        {
            if (shotCount >= 3) break;
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, -2);
                ctx.Result["results"].AsGodotArray().Add(r);
                shotCount++;
            }
        }
    }

    public static void BlindArrow(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }
        var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false);
        ctx.Result["results"].AsGodotArray().Add(r);
        if (r.ContainsKey("hit") && r["hit"].AsBool())
        {
            ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                { "target", target }, { "effect_id", "blind" }, { "duration", 2 },
                { "stat_modifiers", new Godot.Collections.Dictionary { { "attack_bonus", -4 } } }
            });
        }
    }

    public static void TrickArrow(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }
        int dmg = RPGRuleEngine.RollDice(1, 10);
        target.TakeDamage(dmg);
        ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "type", "damage" }, { "target", target }, { "value", dmg }
        });
        string[] debuffs = { "blind", "stun", "fear" };
        string chosen = debuffs[new Random().Next(debuffs.Length)];
        ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
            { "target", target }, { "effect_id", chosen }, { "duration", 1 }
        });
    }

    public static void MeteorShower(in SkillHandlerContext ctx)
    {
        var targets = new List<Vector2I> { ctx.TargetCell };
        targets.AddRange(HexUtils.GetNeighbors(ctx.TargetCell.X, ctx.TargetCell.Y));
        foreach (var pos in targets)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                int dmg = RPGRuleEngine.RollDice(2, 8);
                target.TakeDamage(dmg);
                ctx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "type", "damage" }, { "target", target }, { "value", dmg }, { "damage_type", "ranged" }
                });
            }
        }
    }
}
