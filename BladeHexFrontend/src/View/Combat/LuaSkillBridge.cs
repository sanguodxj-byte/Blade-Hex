// LuaSkillBridge.cs
// Lua 技能执行桥接层 — 负责 SkillHandlerContext ↔ Lua 数据转换（NLua 版）
//
// NLua 的优势：C# 对象直接传给 Lua，无需手动构造 table。
// Lua 脚本通过 ctx.attacker.hp 等方式直接访问 C# 属性。
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using NLua;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Scripting;
using BladeHex.Combat.Skills;

namespace BladeHex.Combat;

/// <summary>
/// Lua 技能桥接器 — 将 C# 战斗上下文转换为 Lua 可用的数据，
/// 并注册需要 Godot Node 引用的 API 函数。
/// </summary>
public static class LuaSkillBridge
{
    private static bool _initialized;

    // 当前执行上下文（在 Execute 调用期间有效）
    private static SkillHandlerContext _currentCtx;
    private static Dictionary<int, Unit> _unitMap = new();
    private static Dictionary<Unit, LuaUnitProxy> _proxyMap = new();
    private static int _nextProxyId;

    // ========================================================================
    // 初始化
    // ========================================================================

    /// <summary>注册 Frontend 层 API 到 Lua 引擎（首次调用时执行）</summary>
    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        var engine = LuaScriptEngine.Instance;
        var lua = engine.Lua;

        // 注册 Core 层 API
        LuaCombatAPI.Register(lua);

        // 注册 Frontend 层 API 对象
        lua["unit"] = new UnitApiObject();
        lua["result"] = new ResultApiObject();
    }

    // ========================================================================
    // 主入口
    // ========================================================================

    /// <summary>
    /// 尝试执行 Lua 技能脚本。
    /// 返回 true 表示找到并执行了脚本（不代表技能效果成功）。
    /// 返回 false 表示没有对应的 Lua 脚本。
    /// </summary>
    public static bool Execute(string skillId, in SkillHandlerContext ctx)
    {
        EnsureInitialized();

        var engine = LuaScriptEngine.Instance;
        if (!engine.HasScript(skillId))
            return false;

        // 设置当前上下文
        _currentCtx = ctx;
        _unitMap.Clear();
        _proxyMap.Clear();
        _nextProxyId = 1;

        // 构造 ctx table
        var ctxTable = BuildContextTable(engine);

        // 执行
        var (success, error) = engine.ExecuteSkill(skillId, ctxTable);

        if (!success && error != null)
        {
            ctx.Result["success"] = false;
            ctx.Result["reason"] = error;
        }

        // 清理
        _unitMap.Clear();
        _proxyMap.Clear();

        return true;
    }

    // ========================================================================
    // 上下文构造
    // ========================================================================

    private static LuaTable BuildContextTable(LuaScriptEngine engine)
    {
        var table = engine.CreateTable();

        table["attacker"] = CreateUnitProxy(_currentCtx.Attacker);
        table["target_q"] = _currentCtx.TargetCell.X;
        table["target_r"] = _currentCtx.TargetCell.Y;

        // enemies / allies 作为 C# 数组（NLua 自动转为可遍历对象）
        var enemyList = new List<LuaUnitProxy>();
        foreach (var u in _currentCtx.Enemies)
            if (GodotObject.IsInstanceValid(u) && u.CurrentHp > 0)
                enemyList.Add(CreateUnitProxy(u));
        table["enemies"] = enemyList.ToArray();

        var allyList = new List<LuaUnitProxy>();
        foreach (var u in _currentCtx.Allies)
            if (GodotObject.IsInstanceValid(u) && u.CurrentHp > 0)
                allyList.Add(CreateUnitProxy(u));
        table["allies"] = allyList.ToArray();

        return table;
    }

    private static LuaUnitProxy CreateUnitProxy(Unit unit)
    {
        if (_proxyMap.TryGetValue(unit, out var existingProxy))
            return existingProxy;

        var proxy = new LuaUnitProxy
        {
            InternalId = _nextProxyId++,
            name = unit.Data?.UnitName ?? "Unknown",
            level = unit.Data?.Level ?? 1,
            is_enemy = unit.Data?.IsEnemy ?? false,
            str = unit.Data?.Str ?? 10,
            dex = unit.Data?.Dex ?? 10,
            con = unit.Data?.Con ?? 10,
            intel = unit.Data?.Intel ?? 10,
            wis = unit.Data?.Wis ?? 10,
            cha = unit.Data?.Cha ?? 10,
            max_hp = unit.GetMaxHp(),
            max_mana = unit.Data != null ? BladeHex.Combat.CombatStats.GetMaxMana(unit.Data) : 0,
            mana = unit.Data?.CurrentMana ?? 0,
            q = unit.GridPos.X,
            r = unit.GridPos.Y,
            facing = unit.Facing,
            ap = unit.CurrentAp,
            max_ap = unit.GetMaxAp(),
            ac = unit.GetAc(),
            attack_bonus = unit.GetAttackBonus(),
            move_range = unit.GetMoveRange(),
            extra_actions = unit.Data?.Runtime.ExtraActionsThisTurn ?? 0,
            life_circle_used = unit.Data?.Runtime.LifeCircleUsedThisCombat ?? 0,
            life_shield_used = unit.Data?.Runtime.LifeShieldUsedThisCombat ?? 0,
            heroic_call_used = unit.Data?.Runtime.HeroicCallUsedThisCombat ?? 0,
            enemy_type = unit.Data?.enemyType.ToString() ?? "",
        };

        // HP 设置（不触发回调）
        proxy.OnHpChanged = null;
        proxy.hp = unit.CurrentHp;

        // 填充状态效果
        if (unit.Data?.Runtime.ActiveStatusEffects != null)
            foreach (var eff in unit.Data.Runtime.ActiveStatusEffects)
                proxy.ActiveEffects.Add(eff.Id);

        // 注册到映射表
        _unitMap[proxy.InternalId] = unit;
        _proxyMap[unit] = proxy;

        // 设置写入回调
        proxy.OnHpChanged = (id, newHp) =>
        {
            if (_unitMap.TryGetValue(id, out var u)) u.SetHp(newHp);
        };
        proxy.OnManaChanged = (id, newMana) =>
        {
            if (_unitMap.TryGetValue(id, out var u) && u.Data != null)
                u.Data.CurrentMana = newMana;
        };

        return proxy;
    }

    // ========================================================================
    // 工具方法
    // ========================================================================

    internal static Unit? ResolveUnit(LuaUnitProxy? proxy)
    {
        if (proxy == null) return null;
        _unitMap.TryGetValue(proxy.InternalId, out var unit);
        return unit;
    }

    internal static SkillHandlerContext GetCurrentContext() => _currentCtx;

    // ========================================================================
    // unit API 对象
    // ========================================================================

    /// <summary>unit 全局对象 — Lua 中通过 unit.find_at(q, r, "enemies") 调用</summary>
    public class UnitApiObject
    {
        public LuaUnitProxy? find_at(int q, int r, string side)
        {
            var pos = new Vector2I(q, r);
            IEnumerable<Unit> searchList = side == "allies"
                ? _currentCtx.Allies
                : _currentCtx.Enemies;

            var found = SkillUtils.FindUnitAt(pos, searchList);
            if (found == null) return null;

            return CreateUnitProxy(found);
        }

        public int heal(LuaUnitProxy proxy, int amount)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return 0;
            int actual = u.Heal(amount);
            proxy.hp = u.CurrentHp;
            return actual;
        }

        public void take_damage(LuaUnitProxy proxy, int amount)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            u.TakeDamage(amount);
            proxy.hp = u.CurrentHp;
        }

        public void change_morale(LuaUnitProxy proxy, int amount)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            MoraleSystem.ChangeMorale(u, amount);
        }

        public bool is_valid(LuaUnitProxy? proxy)
        {
            if (proxy == null) return false;
            var u = ResolveUnit(proxy);
            return u != null && GodotObject.IsInstanceValid(u) && u.CurrentHp > 0;
        }
    }

    // ========================================================================
    // result API 对象
    // ========================================================================

    /// <summary>result 全局对象 — Lua 中通过 result.fail("reason") 调用</summary>
    public class ResultApiObject
    {
        public void add_attack(LuaTable attackTable)
        {
            var dict = LuaTableToGodotDict(attackTable);
            _currentCtx.Result["results"].AsGodotArray().Add(dict);
        }

        public void add_damage(LuaUnitProxy proxy, int value, string? damageType = null)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            var dict = new Godot.Collections.Dictionary
            {
                { "type", "damage" },
                { "target", u },
                { "value", value },
            };
            if (!string.IsNullOrEmpty(damageType))
                dict["damage_type"] = damageType;
            _currentCtx.Result["results"].AsGodotArray().Add(dict);
        }

        public void add_heal(LuaUnitProxy proxy, int value)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            _currentCtx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary
            {
                { "type", "heal" }, { "target", u }, { "value", value }
            });
        }

        public void add_effect(LuaUnitProxy proxy, string effectId, int duration, LuaTable? mods = null)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            var dict = new Godot.Collections.Dictionary
            {
                { "target", u },
                { "effect_id", effectId },
                { "duration", duration },
            };
            if (mods != null)
                dict["stat_modifiers"] = LuaTableToGodotDict(mods);
            _currentCtx.Result["status_effects"].AsGodotArray().Add(dict);
        }

        public void add_remove_effect(LuaUnitProxy proxy, LuaTable idsTable)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            var ids = new Godot.Collections.Array();
            foreach (var key in idsTable.Keys)
            {
                var val = idsTable[key];
                if (val is string s) ids.Add(s);
            }
            _currentCtx.Result["status_effects"].AsGodotArray().Add(new Godot.Collections.Dictionary
            {
                { "target", u },
                { "special", "remove_effects" },
                { "remove_ids", ids },
            });
        }

        public void add_teleport(LuaUnitProxy proxy, int dq, int dr, int oq, int or2)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            _currentCtx.Result["results"].AsGodotArray().Add(new Godot.Collections.Dictionary
            {
                { "type", "teleport" },
                { "target", u },
                { "destination", new Vector2I(dq, dr) },
                { "origin", new Vector2I(oq, or2) },
            });
        }

        public void fail(string reason)
        {
            _currentCtx.Result["success"] = false;
            _currentCtx.Result["reason"] = reason;
        }

        // resolve_attack 放在 result 对象上（需要访问 Grid）
        public LuaTable? resolve_attack(LuaUnitProxy attackerProxy, LuaUnitProxy targetProxy,
            LuaTable? opts = null)
        {
            var attacker = ResolveUnit(attackerProxy);
            var target = ResolveUnit(targetProxy);
            if (attacker == null || target == null) return null;

            bool advantage = false;
            bool disadvantage = false;
            int hitMod = 0;
            float damageMult = 1.0f;
            float nodeFlatScale = 1.0f;

            if (opts != null)
            {
                if (opts["advantage"] is bool adv) advantage = adv;
                if (opts["disadvantage"] is bool dis) disadvantage = dis;
                if (opts["hit_mod"] is long hm) hitMod = (int)hm;
                else if (opts["hit_mod"] is double hmd) hitMod = (int)hmd;
                if (opts["damage_mult"] is double dm) damageMult = (float)dm;
                if (opts["node_flat_scale"] is double nfs) nodeFlatScale = (float)nfs;
            }

            var godotResult = CombatResolver.ResolveAttack(
                attacker, target, _currentCtx.Grid,
                advantage, disadvantage, hitMod, damageMult, null, nodeFlatScale);

            // 转换 Godot Dictionary → Lua table
            return GodotDictToLuaTable(godotResult);
        }
    }

    // ========================================================================
    // 转换工具
    // ========================================================================

    private static Godot.Collections.Dictionary LuaTableToGodotDict(LuaTable table)
    {
        var dict = new Godot.Collections.Dictionary();
        foreach (var key in table.Keys)
        {
            string k = key.ToString()!;
            var val = table[key];
            switch (val)
            {
                case bool b: dict[k] = b; break;
                case long l: dict[k] = (int)l; break;
                case double d:
                    if (d == Math.Floor(d) && d is >= int.MinValue and <= int.MaxValue)
                        dict[k] = (int)d;
                    else
                        dict[k] = d;
                    break;
                case string s: dict[k] = s; break;
                case LuaTable sub: dict[k] = LuaTableToGodotDict(sub); break;
                case LuaUnitProxy proxy:
                    var u = ResolveUnit(proxy);
                    if (u != null) dict[k] = u;
                    break;
            }
        }
        return dict;
    }

    private static LuaTable? GodotDictToLuaTable(Godot.Collections.Dictionary dict)
    {
        var engine = LuaScriptEngine.Instance;
        var table = engine.CreateTable();
        foreach (var key in dict.Keys)
        {
            string k = key.AsString();
            var val = dict[key];
            switch (val.VariantType)
            {
                case Variant.Type.Bool: table[k] = val.AsBool(); break;
                case Variant.Type.Int: table[k] = val.AsInt64(); break;
                case Variant.Type.Float: table[k] = val.AsDouble(); break;
                case Variant.Type.String: table[k] = val.AsString(); break;
                // 复杂类型（Unit 引用等）不传给 Lua
            }
        }
        return table;
    }
}
