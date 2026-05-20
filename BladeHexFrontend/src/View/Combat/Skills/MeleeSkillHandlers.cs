// MeleeSkillHandlers.cs
// 近战主动技能 handler — STR 系 + 近战 CON 系
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.Combat.Skills;

/// <summary>近战主动技能执行器</summary>
public static class MeleeSkillHandlers
{
    public static void DoubleAttack(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }

        var r1 = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false);
        ctx.Result["results"].AsGodotArray().Add(r1);
        var r2 = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, -3);
        ctx.Result["results"].AsGodotArray().Add(r2);
    }

    public static void Whirlwind(in SkillHandlerContext ctx)
    {
        if (ctx.Grid == null) return;
        var neighbors = HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y);
        foreach (var pos in neighbors)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                // v0.6 11.8: 旋风斩节点平伤对每个目标按 50% 结算
                var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, 0, 1.0f, null, 0.5f);
                ctx.Result["results"].AsGodotArray().Add(r);
            }
        }
    }

    public static void BattleCry(in SkillHandlerContext ctx)
    {
        var neighbors = ctx.Grid != null ? HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y) : Array.Empty<Vector2I>();
        foreach (var pos in neighbors)
        {
            var enemy = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (enemy != null)
            {
                ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                    { "target", enemy }, { "effect_id", "fear" }, { "duration", 2 },
                    { "stat_modifiers", new Godot.Collections.Dictionary { { "attack_bonus", -2 } } }
                });
            }
        }
        foreach (var ally in ctx.Allies)
        {
            if (GodotObject.IsInstanceValid(ally) && ally.CurrentHp > 0)
                MoraleSystem.ChangeMorale(ally, 3);
        }
    }

    public static void BloodVortex(in SkillHandlerContext ctx)
    {
        if (ctx.Grid == null) return;
        var neighbors = HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y);
        // v0.6 11.8 限制：每次使用最多恢复 3d6 HP
        int healCap = RPGRuleEngine.RollDice(3, 6);
        int healed = 0;
        foreach (var pos in neighbors)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                // v0.6 11.4.2 / 11.8 节点平伤 AOE 副目标 ×0.5
                var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, 0, 1.0f, null, 0.5f);
                ctx.Result["results"].AsGodotArray().Add(r);
                if (r.ContainsKey("hit") && r["hit"].AsBool() && healed < healCap)
                {
                    int gain = System.Math.Min(RPGRuleEngine.RollDice(1, 6), healCap - healed);
                    ctx.Attacker.Heal(gain);
                    healed += gain;
                }
            }
        }
    }

    public static void Bloodthirst(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }

        // v0.6 11.8 嗜血节点：被动触发，每回合最多 1 次
        if (ctx.Attacker.Data?.Runtime.ExtraActionsThisTurn > 0)
        {
            SkillUtils.Fail(ctx.Result, "本回合已获得过额外行动");
            return;
        }

        var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false);
        ctx.Result["results"].AsGodotArray().Add(r);
        bool didKill = target.CurrentHp <= 0
            || (r.ContainsKey("hit") && r["hit"].AsBool()
                && target.CurrentHp - (r.ContainsKey("damage") ? r["damage"].AsInt32() : 0) <= 0);
        if (didKill && ctx.Attacker.Data != null)
        {
            // v0.6 11.8: 击杀后获得 4 AP 额外行动池（仅可用于普攻 / 移动，不能技能 / Spell）
            ctx.Attacker.Data.Runtime.ExtraActionsThisTurn += 1;
            ctx.Attacker.Data.Runtime.CurrentAp += 4;
            ctx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary {
                { "target", ctx.Attacker }, { "effect_id", "bloodthirst_extra_action" }, { "duration", 1 },
                { "stat_modifiers", new Godot.Collections.Dictionary { { "extra_ap", 4 } } }
            });
        }
    }

    public static void SwordDance(in SkillHandlerContext ctx)
    {
        if (ctx.Grid == null) return;
        var neighbors = HexUtils.GetNeighbors(ctx.Attacker.GridPos.X, ctx.Attacker.GridPos.Y);
        foreach (var pos in neighbors)
        {
            var target = SkillUtils.FindUnitAt(pos, ctx.Enemies);
            if (target != null)
            {
                // AOE：节点平伤副目标 ×0.5
                var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false, false, 0, 1.5f, null, 0.5f);
                ctx.Result["results"].AsGodotArray().Add(r);
            }
        }
    }

    public static void ShieldBash(in SkillHandlerContext ctx)
    {
        var target = SkillUtils.FindUnitAt(ctx.TargetCell, ctx.Enemies);
        if (target == null) { SkillUtils.Fail(ctx.Result, "目标格没有敌人"); return; }

        var r = CombatResolver.ResolveAttack(ctx.Attacker, target, ctx.Grid, false);
        ctx.Result["results"].AsGodotArray().Add(r);
        if (r.ContainsKey("hit") && r["hit"].AsBool() && ctx.Grid != null)
        {
            int facing = ctx.Attacker.Data!.Runtime.Facing;
            var pushTarget = HexUtils.GetNeighbor(target.GridPos.X, target.GridPos.Y, facing);
            var pushCell = ctx.Grid.GetCell(pushTarget.X, pushTarget.Y);
            if (pushCell != null && pushCell.Occupant == null)
            {
                var oldCell = ctx.Grid.GetCell(target.GridPos.X, target.GridPos.Y);
                if (oldCell != null) oldCell.Occupant = null;
                pushCell.Occupant = target;
                target.GridPos = pushTarget;
            }
        }
    }
}
