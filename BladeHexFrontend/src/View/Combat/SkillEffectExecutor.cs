using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Combat.Skills;

namespace BladeHex.Combat;

/// <summary>
/// 技能特效执行引擎 — 注册表分发器
/// 通过 handler 注册表将技能 ID 映射到具体执行方法。
/// 具体实现分布在 Skills/ 子目录的各 Handler 类中。
/// </summary>
public static class SkillEffectExecutor
{
    // ============================================================================
    // Handler 委托类型
    // ============================================================================

    /// <summary>技能执行 handler 签名</summary>
    public delegate void SkillHandler(in SkillHandlerContext ctx);

    // ============================================================================
    // Handler 注册表
    // ============================================================================

    private static readonly Dictionary<string, SkillHandler> Handlers = new()
    {
        // --- 全部主动技能已迁移到 Lua: scripts/skills/ ---
        // C# 注册表仅保留无法迁移的特殊条目

        // --- 法术研习槽（UI 层特殊处理，不走 Lua）---
        ["spell_slot_1"] = StubSpellSlot,
        ["spell_slot_2"] = StubSpellSlot,
        ["spell_slot_3"] = StubSpellSlot,
        ["spell_slot_4"] = StubSpellSlot,
        ["spell_slot_5"] = StubSpellSlot,

        // --- 占位 stub（等法表系统实装）---
        ["ward_blessing"] = StubNoOp,
        ["purify_field"] = StubNoOp,

        // --- 复活（设计上禁用）---
        ["resurrect"] = SupportSkillHandlers.Resurrect,
    };

    // ============================================================================
    // 法表 v1.3 临时占位 stub
    // ============================================================================

    /// <summary>
    /// 法术研习槽节点的 stub。spell_slot_X 不通过 UseSkill 调用 — 节点激活时
    /// UI 层调出"选系面板"，从该环位的 5 系（毁灭/幻术/附魔/防护/生命）中选 1 个，
    /// 把对应法术写入 LearnedSpells。详见 docs/法表系统.md §5.1
    /// </summary>
    private static void StubSpellSlot(in SkillHandlerContext ctx)
    {
        SkillUtils.Fail(ctx.Result, "法术槽节点 — 请通过节点激活面板选择法术学派");
    }

    /// <summary>
    /// 占位 stub：等法表系统实装后替换为正式 handler。
    /// </summary>
    private static void StubNoOp(in SkillHandlerContext ctx)
    {
        SkillUtils.Fail(ctx.Result, "技能尚未实装");
    }


    // ============================================================================
    // 基础接口（保持向后兼容）
    // ============================================================================

    public static Godot.Collections.Dictionary GetSkillConfig(string skillEffect) => SkillRegistry.GetSkillConfig(skillEffect);
    public static bool IsActiveSkill(string skillEffect) => SkillRegistry.IsActiveSkill(skillEffect);
    public static bool IsPassiveSkill(string skillEffect) => SkillRegistry.IsPassiveSkill(skillEffect);

    // ============================================================================
    // 主动技能执行 — 主入口（注册表分发）
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

        // 注册表分发：C# Handler 优先，Lua 脚本 fallback
        var ctx = new SkillHandlerContext
        {
            Attacker = attacker,
            TargetCell = targetCell,
            Grid = grid,
            Enemies = enemies,
            Allies = allies,
            Result = result,
        };

        if (Handlers.TryGetValue(skillEffect, out var handler))
        {
            handler(in ctx);
        }
        else if (LuaSkillBridge.Execute(skillEffect, in ctx))
        {
            // Lua 脚本已处理（成功或失败都由脚本内部设置 result）
        }
        else
        {
            result["success"] = false;
            result["reason"] = $"技能 {skillEffect} 逻辑尚未注册";
        }

        return result;
    }

    // ============================================================================
    // 向后兼容
    // ============================================================================

    public static bool HasQuickCast(Unit unit) => PassiveSkillResolver.HasQuickCast(unit);
}
