// CareerSkillExecutor.cs
// 职业专属技能执行引擎 — v1.0: 直接执行 v1 分阶职业技能
//
// v1.0 与 v0.8 的关键区别:
//   - 不再调用 LuaSkillBridge (旧 Lua 脚本为 v0.8 遗产)
//   - 根据 CareerSkillData 的 RequiresFullAp / ConsumesMaxAp / LimitType 决定释放规则
//   - 五属性主动: 满 AP 才能用, 消耗最大 AP, 每场 1 次
//   - 六属性主动: 每回合 1 次, 代类型, 不消耗 AP
//   - 一至四属性被动由 CareerPassiveHookSystem 接钩子执行, 本类不处理
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Strategic;
using BladeHex.Combat.Skills;
using BladeHex.Combat.Buff;

namespace BladeHex.Combat;

/// <summary>
/// 职业专属技能执行引擎 — v1.0
/// </summary>
public static class CareerSkillExecutor
{
    // ============================================================================
    // 主入口
    // ============================================================================

    /// <summary>执行职业技能 (v1.0 入口)</summary>
    public static SkillExecutionResult ExecuteCareerSkill(
        Unit caster,
        Vector2I targetCell,
        HexGrid? grid,
        IEnumerable<Unit> allUnits,
        IEnumerable<Unit> playerUnits,
        IEnumerable<Unit> enemyUnits)
    {
        var skill = caster.GetCareerSkill();
        if (skill == null)
            return SkillExecutionResult.Fail("没有当前职业称号对应的职业技能");

        if (!skill.IsActive)
            return SkillExecutionResult.Fail("当前职业技能为被动, 不可主动释放");

        // ---- 使用次数校验 ----
        if (!caster.CanUseCareerSkill())
        {
            if (skill.IsOncePerBattle)
                return SkillExecutionResult.Fail("职业技能本场战斗已使用过");
            if (skill.IsOncePerTurn)
                return SkillExecutionResult.Fail("职业技能本回合已使用过");
            return SkillExecutionResult.Fail("职业技能不可用");
        }

        // ---- AP 条件校验 ----
        if (skill.RequiresFullAp)
        {
            if (caster.CurrentAp < caster.GetMaxAp() - 0.01f)
                return SkillExecutionResult.Fail("需要 AP 全满才能使用");
        }

        // ---- 额外条件校验 (五属性专属) ----
        if (skill.EffectId == "lone_star_shadow" && grid != null)
        {
            var hostiles = allUnits.Where(u => u.Data != null && u.Data.IsEnemy != caster.Data?.IsEnemy);
            int soloRange = skill.EffectParams.GetValueOrDefault("solo_range", 6).AsInt32();
            var hostileCount = CountEnemiesWithin(caster, hostiles, soloRange);
            if (hostileCount != 1)
                return SkillExecutionResult.Fail("孤星之刃影: 周围 6 格内必须有且仅有 1 个敌方单位");
        }

        // ---- 按 effectId 分发执行 ----
        var allies = !caster.Data!.IsEnemy ? playerUnits : enemyUnits;
        var enemies = !caster.Data!.IsEnemy ? enemyUnits : playerUnits;

        SkillExecutionResult result;

        switch (skill.EffectId)
        {
            case "emissary_pact_seal":
                result = ExecuteEmissary(caster, targetCell, allUnits);
                break;
            case "mountain_throne":
                result = ExecuteMountainThrone(caster);
                break;
            case "astral_rift":
                result = ExecuteAstralRift(caster, targetCell, grid, enemies);
                break;
            case "waste_avatar":
                result = ExecuteWasteAvatar(caster);
                break;
            case "iron_blood_edict":
                result = ExecuteIronBloodEdict(allies);
                break;
            case "lone_star_shadow":
                result = ExecuteLoneStarShadow(caster, targetCell, allUnits, grid);
                break;
            case "paragon_all_aspects":
                result = ExecuteParagon(caster);
                break;
            default:
                return SkillExecutionResult.Fail($"未知的职业技能效果: {skill.EffectId}");
        }

        // ---- 后处理: 消耗与记录 ----
        if (result.Success)
        {
            if (skill.ConsumesMaxAp)
            {
                // 消耗全部 AP (清零)
                caster.ConsumeAp(caster.CurrentAp);
            }
            caster.RecordCareerSkillUse();
        }

        return result;
    }

    // ============================================================================
    // 五属性主动技能实现 (6 个)
    // ============================================================================

    /// <summary>万灵之约印: 目标友军回满 HP、法力、AP</summary>
    private static SkillExecutionResult ExecuteEmissary(
        Unit caster, Vector2I targetCell, IEnumerable<Unit> allUnits)
    {
        var target = FindUnitAt(targetCell, allUnits);
        if (target == null)
            return SkillExecutionResult.Fail("目标位置没有单位");

        if (target.Data!.IsEnemy == caster.Data!.IsEnemy)
        {
            // 友军
            int healAmount = target.Model.GetMaxHp() - target.CurrentHp;
            target.CurrentHp = target.Model.GetMaxHp();
            target.Data.CurrentMana = CombatStats.GetMaxMana(target.Data);
            target.CurrentAp = target.GetMaxAp();

            return SkillExecutionResult.Ok(
                new ResultText($"万灵之约印: {target.Data.UnitName} 恢复全满状态"),
                new HealEvent(target.Model, healAmount));
        }

        return SkillExecutionResult.Fail("万灵之约印: 目标必须是友军");
    }

    /// <summary>山岳之王座: 3 回合不动如山状态</summary>
    private static SkillExecutionResult ExecuteMountainThrone(Unit caster)
    {
        if (caster.Data?.Runtime == null)
            return SkillExecutionResult.Fail("山岳之王座: 缺少运行时状态");

        caster.Data.Runtime.CareerMountainThroneTurns = 3;

        return SkillExecutionResult.Ok(
            new ResultText("山岳之王座: 获得 3 回合不动如山"),
            new BuffApplication("mountain_throne_immovable", caster.Model));
    }

    /// <summary>星界之裂隙: 直线跳跃到目标格, 攻击路径上所有敌人</summary>
    private static SkillExecutionResult ExecuteAstralRift(
        Unit caster, Vector2I targetCell, HexGrid? grid, IEnumerable<Unit> enemies)
    {
        if (grid == null)
            return SkillExecutionResult.Fail("星界之裂隙: 需要战斗地图");
        if (caster.Data?.Runtime == null)
            return SkillExecutionResult.Fail("星界之裂隙: 缺少运行时状态");

        // 校验是否直线
        var start = caster.GridPos;
        if (!IsLinePath(start, targetCell))
            return SkillExecutionResult.Fail("星界之裂隙: 目标必须在直线方向上");

        // 获取路径上的格子（不含起点, 含终点）
        var pathCells = GetLineCells(start, targetCell);
        if (pathCells.Count == 0)
            return SkillExecutionResult.Fail("星界之裂隙: 路径无效");

        // 更新地图格子占用 (离开旧格)
        var startCell = grid.GetCell(start.X, start.Y);
        if (startCell != null && startCell.Occupant == caster)
            startCell.Occupant = null;

        // 移动自身到目标格
        caster.GridPos = targetCell;

        // 更新地图格子占用 (占据新格)
        var destCell = grid.GetCell(targetCell.X, targetCell.Y);
        if (destCell != null)
            destCell.Occupant = caster;

        // 对路径格上所有敌人各攻击一次
        var subResults = new List<SkillSubResult>();
        int attacked = 0;
        int totalDamage = 0;
        foreach (var cell in pathCells)
        {
            var enemy = FindUnitAt(cell, enemies);
            if (enemy != null)
            {
                attacked++;
                var attackResult = CombatResolver.ResolveAttack(caster, enemy, grid);
                bool hit = attackResult.ContainsKey("hit") && attackResult["hit"].AsBool();
                bool isCrit = attackResult.ContainsKey("critical") && attackResult["critical"].AsBool();
                int dmg = 0;
                if (attackResult.ContainsKey("damage"))
                {
                    dmg = attackResult["damage"].AsInt32();
                    totalDamage += dmg;
                }

                if (hit && dmg > 0)
                {
                    bool killingBlow = enemy.CurrentHp <= 0;
                    subResults.Add(new DamageEvent(enemy.Model, dmg, isCrit, killingBlow));
                }
                subResults.Add(new ResultText(
                    $"星界之裂隙攻击 {enemy.Data?.UnitName ?? "敌人"}: {(hit ? $"{dmg} 伤害" : "未命中")}"));
            }
        }

        // 传送事件
        subResults.Add(new TeleportEvent(caster.Model, targetCell, start));

        // 汇总文本
        subResults.Add(new ResultText(
            $"星界之裂隙: 跳跃到 ({targetCell.X},{targetCell.Y}), 路径上 {attacked} 个敌人受攻击, 总伤害 {totalDamage}"));

        return SkillExecutionResult.Ok(subResults.ToArray());
    }

    /// <summary>荒芜之化身: 本场战斗紧邻敌人护甲 100% 被穿透</summary>
    private static SkillExecutionResult ExecuteWasteAvatar(Unit caster)
    {
        if (caster.Data?.Runtime == null)
            return SkillExecutionResult.Fail("荒芜化身: 缺少运行时状态");

        caster.Data.Runtime.CareerWrathArmorPenActive = true;

        return SkillExecutionResult.Ok(
            new ResultText("荒芜之化身: 本场战斗紧邻敌人护甲 100% 穿透"),
            new BuffApplication("waste_avatar_armor_pen", caster.Model));
    }

    /// <summary>铁血之律令: 全体友军获得 1 次未命中转暴击</summary>
    private static SkillExecutionResult ExecuteIronBloodEdict(IEnumerable<Unit> allies)
    {
        var subResults = new List<SkillSubResult>();
        int affected = 0;
        foreach (var ally in allies)
        {
            if (ally.Data?.Runtime == null) continue;
            ally.Data.Runtime.CareerIronEdictPendingCount++;
            affected++;
            subResults.Add(new BuffApplication("iron_blood_edict", ally.Model));
        }

        subResults.Insert(0, new ResultText($"铁血之律令: {affected} 名友军获得铁血效果"));

        return SkillExecutionResult.Ok(subResults.ToArray());
    }

    /// <summary>孤星之刃影: 锁定周围唯一敌人, 必中必暴</summary>
    private static SkillExecutionResult ExecuteLoneStarShadow(
        Unit caster, Vector2I targetCell, IEnumerable<Unit> allUnits, HexGrid? grid)
    {
        var target = FindUnitAt(targetCell, allUnits);
        if (target == null)
            return SkillExecutionResult.Fail("孤星之刃影: 目标位置没有单位");

        if (target.Data!.IsEnemy == caster.Data!.IsEnemy)
            return SkillExecutionResult.Fail("孤星之刃影: 目标必须是敌人");

        // 校验周围 6 格只有 1 个敌人
        if (CountEnemiesWithin(caster, allUnits.Where(u => u.Data!.IsEnemy != caster.Data!.IsEnemy), 6) != 1)
            return SkillExecutionResult.Fail("孤星之刃影: 周围 6 格内必须有且仅有 1 个敌方单位");

        if (caster.Data?.Runtime == null)
            return SkillExecutionResult.Fail("孤星之刃影: 缺少运行时状态");

        // 记录锁定标记 (CareerPassiveHookSystem 读取)
        caster.Data.Runtime.CareerLoneShadowLockedTarget = (ulong)target.GetInstanceId();

        return SkillExecutionResult.Ok(
            new ResultText($"孤星之刃影: 锁定目标 {target.Data.UnitName}, 对其必中必暴"),
            new BuffApplication("lone_star_shadow_lock", caster.Model));
    }

    // ============================================================================
    // 六属性(万象)主动技能实现
    // ============================================================================

    /// <summary>万象: 代价型主动, 抽取不重复代价, 恢复 HP/法力/AP</summary>
    private static SkillExecutionResult ExecuteParagon(Unit caster)
    {
        if (caster.Data == null)
            return SkillExecutionResult.Fail("万象: 缺少单位数据");
        var rt = caster.Data.Runtime;
        if (rt == null)
            return SkillExecutionResult.Fail("万象: 缺少运行时状态");

        if (rt.CareerParagonExhausted)
            return SkillExecutionResult.Fail("万象: 三个代价已抽完, 本场战斗不可再使用");

        // 找到下一个未抽中的代价
        string[] allCosts = ["no_move", "no_spell", "no_attack"];
        int costIndex = -1;

        for (int i = 0; i < allCosts.Length; i++)
        {
            if ((rt.CareerParagonCostsMask & (1 << i)) == 0)
            {
                costIndex = i;
                break;
            }
        }

        if (costIndex < 0)
        {
            rt.CareerParagonExhausted = true;
            return SkillExecutionResult.Fail("万象: 所有代价已抽完");
        }

        // 标记该代价已抽中
        rt.CareerParagonCostsMask |= (1 << costIndex);

        // 恢复 HP、法力、AP
        caster.CurrentHp = caster.Model.GetMaxHp();
        caster.Data.CurrentMana = CombatStats.GetMaxMana(caster.Data);
        caster.CurrentAp = caster.GetMaxAp();

        // 检查是否所有代价已抽完
        if (rt.CareerParagonCostsMask == 0b111)
            rt.CareerParagonExhausted = true;

        var costName = allCosts[costIndex] switch
        {
            "no_move" => "无法移动",
            "no_spell" => "无法施法",
            "no_attack" => "无法攻击",
            _ => "未知代价"
        };

        return SkillExecutionResult.Ok(
            new ResultText($"万象: 抽取代价[{costName}], 恢复全部 HP/法力/AP"));
    }

    // ============================================================================
    // 辅助方法
    // ============================================================================

    private static Unit? FindUnitAt(Vector2I pos, IEnumerable<Unit> units)
        => units.FirstOrDefault(u => u.GridPos == pos);

    /// <summary>计算指定范围内敌方数量</summary>
    private static int CountEnemiesWithin(Unit caster, IEnumerable<Unit> enemies, int range)
    {
        int count = 0;
        foreach (var enemy in enemies)
        {
            if (enemy == caster) continue;
            if (!GodotObject.IsInstanceValid(enemy) || enemy.CurrentHp <= 0) continue;
            if (caster.DistanceTo(enemy) <= range)
                count++;
        }
        return count;
    }

    /// <summary>判断起点到终点是否为直线 (轴向/对角线)</summary>
    private static bool IsLinePath(Vector2I from, Vector2I to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return dx == 0 || dy == 0 || Math.Abs(dx) == Math.Abs(dy);
    }

    /// <summary>获取两点间直线路径上的格子 (不含起点, 含终点)</summary>
    private static List<Vector2I> GetLineCells(Vector2I from, Vector2I to)
    {
        var cells = new List<Vector2I>();
        var dx = Math.Sign(to.X - from.X);
        var dy = Math.Sign(to.Y - from.Y);
        var cx = from.X + dx;
        var cy = from.Y + dy;
        while (cx != to.X || cy != to.Y)
        {
            cells.Add(new Vector2I(cx, cy));
            cx += dx;
            cy += dy;
        }
        cells.Add(to);
        return cells;
    }
}
