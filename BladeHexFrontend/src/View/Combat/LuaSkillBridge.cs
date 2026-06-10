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
        lua["buff"] = new BuffApiObject();
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
            _currentCtx.Builder.Fail(error);
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

        // v0.8: 注入 tier_multiplier / attribute_count / consume_all_ap
        var skill = _currentCtx.Attacker.GetCareerSkill();
        if (skill != null)
        {
            table["tier"] = (double)skill.EffectParams.GetValueOrDefault("tier_multiplier", 1.0f).AsSingle();
            table["attribute_count"] = skill.EffectParams.GetValueOrDefault("attribute_count", 1).AsInt32();
            table["consume_all_ap"] = skill.EffectParams.GetValueOrDefault("consume_all_ap", true).AsBool();
            table["effect_id"] = skill.EffectId;
        }
        else
        {
            table["tier"] = 1.0;
            table["attribute_count"] = 1;
            table["consume_all_ap"] = true;
            table["effect_id"] = "";
        }

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

        // grid 引用（供 hex API 使用）
        table["has_grid"] = _currentCtx.Grid != null;

        return table;
    }

    private static LuaUnitProxy CreateUnitProxy(Unit unit)
    {
        if (_proxyMap.TryGetValue(unit, out var existingProxy))
            return existingProxy;

        var proxy = new LuaUnitProxy
        {
            InternalId = _nextProxyId++,
            instance_id = (long)unit.GetInstanceId(),
            character_id = unit.Data?.CharacterId ?? -1,
            name = unit.Data?.UnitName ?? "Unknown",
            level = unit.Data?.Level ?? 1,
            is_enemy = unit.Data?.IsEnemy ?? false,
            str = CombatStats.GetEffectiveStr(unit.Data),
            dex = CombatStats.GetEffectiveDex(unit.Data),
            con = CombatStats.GetEffectiveCon(unit.Data),
            intel = CombatStats.GetEffectiveInt(unit.Data),
            wis = CombatStats.GetEffectiveWis(unit.Data),
            cha = CombatStats.GetEffectiveCha(unit.Data),
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
        proxy.OnApChanged = null;
        proxy.OnExtraActionsChanged = null;
        proxy.hp = unit.CurrentHp;

        // 填充状态效果。迁移期同时读取旧 StatusEffect 与新 Buff,让 Lua 的
        // proxy:has_effect("...") 对两套运行时状态保持一致。
        if (unit.Data != null)
        {
            foreach (var eff in unit.Model.ActiveStatusEffects)
                proxy.ActiveEffects.Add(eff.Id);
            foreach (var buff in unit.Model.ActiveBuffs)
                proxy.ActiveEffects.Add(buff.Id);
        }

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
                u.Model.CurrentMana = newMana;
        };
        proxy.OnApChanged = (id, newAp) =>
        {
            if (_unitMap.TryGetValue(id, out var u)) u.CurrentAp = newAp;
        };
        proxy.OnExtraActionsChanged = (id, value) =>
        {
            if (_unitMap.TryGetValue(id, out var u) && u.Data != null)
                u.Model.ExtraActionsThisTurn = value;
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
            int actual = u.Heal(amount, _currentCtx.Attacker);
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

        public bool is_valid(LuaUnitProxy? proxy)
        {
            if (proxy == null) return false;
            var u = ResolveUnit(proxy);
            return u != null && GodotObject.IsInstanceValid(u) && u.CurrentHp > 0;
        }

        public bool can_push_to(int q, int r)
        {
            var grid = _currentCtx.Grid;
            if (grid == null) return true;
            var cell = grid.GetCell(q, r);
            if (cell == null) return false;
            if (cell.Occupant != null) return false;
            return cell.Data == null || cell.Data.isPassable;
        }

        /// <summary>传送单位到指定位置 (Lua: unit.teleport(proxy, q, r))</summary>
        public void teleport(LuaUnitProxy proxy, int q, int r)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            var oldPos = u.GridPos;
            u.GridPos = new Vector2I(q, r);
            proxy.q = q;
            proxy.r = r;
            _currentCtx.Builder.AddTeleport(u, new Vector2I(q, r), oldPos);
        }

        /// <summary>获取单位距离 (Lua: unit.distance(proxy1, proxy2))</summary>
        public int distance(LuaUnitProxy a, LuaUnitProxy b)
            => HexUtils.Distance(a.q, a.r, b.q, b.r);

        /// <summary>获取六角方向向量 (Lua: unit.direction(dir))</summary>
        public Vector2I direction(int dir) => HexUtils.Directions[dir];

        /// <summary>获取邻格坐标 (Lua: unit.get_neighbor(q, r, dir))</summary>
        public Vector2I get_neighbor(int q, int r, int dir) => HexUtils.GetNeighbor(q, r, dir);
    }

    // ========================================================================
    // buff API 对象
    // ========================================================================

    /// <summary>
    /// buff 全局对象 — Lua 中通过 buff.apply(target, "burning", 2) 直接调用新 BuffSystem。
    /// 这是迁移期 API：让新 Lua 技能可以绕开旧 status_effects 协议，但不强制迁移旧脚本。
    /// </summary>
    public class BuffApiObject
    {
        public bool apply(LuaUnitProxy proxy, string buffId, int duration = -1, string source = "")
            => ApplyInternal(proxy, buffId, duration, null, source);

        public bool apply_custom(LuaUnitProxy proxy, string buffId, int duration = -1, LuaTable? mods = null, string source = "")
            => ApplyInternal(proxy, buffId, duration, mods, source);

        private static bool ApplyInternal(LuaUnitProxy proxy, string buffId, int duration, LuaTable? mods, string source)
        {
            var u = ResolveUnit(proxy);
            if (u?.Data == null || string.IsNullOrEmpty(buffId)) return false;
            if (PassiveSkillResolver.IsFearEffect(buffId)
                && !PassiveSkillResolver.CanApplyFearEffect(u, u.CombatManager?.AllUnits))
                return false;

            string actualSource = string.IsNullOrEmpty(source) ? "lua_skill" : source;
            int sourceUnitId = GodotObject.IsInstanceValid(_currentCtx.Attacker)
                ? (int)_currentCtx.Attacker.GetInstanceId()
                : -1;

            Buff.BuffInstance? applied;
            if (mods == null)
            {
                applied = Buff.BuffSystem.Apply(u.Data, buffId, duration, sourceUnitId, actualSource);
            }
            else
            {
                var template = Buff.BuffRegistry.Get(buffId);
                var instance = template != null
                    ? CloneBuffTemplate(template)
                    : new Buff.BuffInstance { Id = buffId, Name = buffId, Description = buffId };

                instance.Id = buffId;
                if (string.IsNullOrEmpty(instance.Name)) instance.Name = buffId;
                if (template == null && _currentCtx.Attacker?.Data != null && u.Data != null)
                    instance.IsNegative = u.Data.IsEnemy != _currentCtx.Attacker.Data.IsEnemy;
                instance.Duration = duration > 0 ? duration : instance.Duration;
                if (instance.Duration == 0) instance.Duration = duration;
                instance.SourceUnitId = sourceUnitId;
                instance.Source = actualSource;
                instance.Modifiers = LuaTableToStatModifiers(mods);

                applied = Buff.BuffSystem.ApplyDirect(u.Data!, instance);
                if (applied != null)
                    ApplyImmediateRuntimeModifiers(u, proxy, instance.Modifiers);
            }

            if (applied == null) return false;

            proxy.ActiveEffects.Add(applied.Id);
            NotifyBuffChanged(u);
            return true;
        }

        public bool remove(LuaUnitProxy proxy, string buffId)
        {
            var u = ResolveUnit(proxy);
            if (u?.Data == null || string.IsNullOrEmpty(buffId)) return false;

            bool removed = Buff.BuffSystem.Remove(u.Data, buffId);
            removed |= u.Model.RemoveStatusEffect(buffId);

            if (removed)
            {
                BladeHex.Events.EventBus.Instance?.Publish(BladeHex.Events.EventBus.Signals.StatusEffectRemoved,
                    new Godot.Collections.Dictionary { { "unit", u }, { "effect_id", buffId } });
            }

            if (removed)
            {
                proxy.ActiveEffects.Remove(buffId);
                NotifyBuffChanged(u);
            }
            return removed;
        }

        public bool remove_source(LuaUnitProxy proxy, string source)
        {
            var u = ResolveUnit(proxy);
            if (u?.Data == null || string.IsNullOrEmpty(source)) return false;

            bool removed = Buff.BuffSystem.RemoveBySource(u.Data, source);
            if (removed)
            {
                BladeHex.Events.EventBus.Instance?.Publish(BladeHex.Events.EventBus.Signals.StatusEffectRemoved,
                    new Godot.Collections.Dictionary { { "unit", u }, { "effect_id", source } });
                NotifyBuffChanged(u);
            }
            return removed;
        }

        public bool has(LuaUnitProxy proxy, string buffId)
        {
            var u = ResolveUnit(proxy);
            return u?.Data != null && Buff.BuffSystem.HasBuff(u.Data, buffId);
        }

        public bool has_tag(LuaUnitProxy proxy, string tag)
        {
            var u = ResolveUnit(proxy);
            return u?.Data != null && Buff.BuffSystem.HasTag(u.Data, tag);
        }

        public int stacks(LuaUnitProxy proxy, string buffId)
        {
            var u = ResolveUnit(proxy);
            return u?.Data != null ? Buff.BuffSystem.GetStacks(u.Data, buffId) : 0;
        }

        public void remove_many(LuaUnitProxy proxy, LuaTable idsTable)
        {
            foreach (var key in idsTable.Keys)
                if (idsTable[key] is string id)
                    remove(proxy, id);
        }

        private static void NotifyBuffChanged(Unit unit)
        {
            CharacterRenderBus.Instance?.NotifyStatusEffects(unit, new Godot.Collections.Array());
        }

        private static void ApplyImmediateRuntimeModifiers(Unit unit, LuaUnitProxy proxy, List<Buff.StatModifier> modifiers)
        {
            if (unit.Data == null) return;
            foreach (var modifier in modifiers)
            {
                switch (modifier.Stat)
                {
                    case "extra_ap":
                        unit.CurrentAp += modifier.Value;
                        proxy.ap = unit.CurrentAp;
                        break;
                    case "extra_action":
                        if (modifier.Value != 0f)
                        {
                            unit.Model.ExtraActionsThisTurn += 1;
                            proxy.extra_actions = unit.Model.ExtraActionsThisTurn;
                        }
                        break;
                }
            }
        }
    }

    // ========================================================================
    // result API 对象
    // ========================================================================

    /// <summary>result 全局对象 — Lua 中通过 result.fail("reason") 调用</summary>
    public class ResultApiObject
    {
        public void add_attack(LuaTable attackTable, LuaUnitProxy? targetProxy = null)
        {
            var u = targetProxy != null ? ResolveUnit(targetProxy) : ResolveAttackTarget(attackTable);
            if (u == null) return;

            // 从 Lua table 提取攻击结果字段
            bool hit = attackTable["hit"] is bool b && b;
            if (!hit) return;

            int damage = 0;
            var dmgVal = attackTable["damage"];
            if (dmgVal is long dl) damage = (int)dl;
            else if (dmgVal is double dd) damage = (int)dd;

            bool critical = attackTable["critical"] is bool cb && cb;
            bool killingBlow = attackTable["killing_blow"] is bool kb && kb;

            _currentCtx.Builder.AddDamage(u, damage, critical, killingBlow);
        }

        private static Unit? ResolveAttackTarget(LuaTable attackTable)
        {
            var targetIdValue = attackTable["__target_internal_id"];
            int targetId = targetIdValue switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                _ => 0,
            };

            return targetId > 0 && _unitMap.TryGetValue(targetId, out var u) ? u : null;
        }

        public void add_damage(LuaUnitProxy proxy, int value, string? damageType = null)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            _currentCtx.Builder.AddDamage(u, value);
        }

        public void add_heal(LuaUnitProxy proxy, int value)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            _currentCtx.Builder.AddHeal(u, value);
        }

        public void add_effect(LuaUnitProxy proxy, string effectId, int duration, LuaTable? mods = null)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            _currentCtx.Builder.AddStatusEffect(effectId, u, duration);
        }

        public void add_remove_effect(LuaUnitProxy proxy, LuaTable idsTable)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            foreach (var key in idsTable.Keys)
            {
                if (idsTable[key] is string id)
                    _currentCtx.Builder.AddRemoveEffect(u, id);
            }
        }

        public void add_teleport(LuaUnitProxy proxy, int dq, int dr, int oq, int or2)
        {
            var u = ResolveUnit(proxy);
            if (u == null) return;
            _currentCtx.Builder.AddTeleport(u, new Vector2I(dq, dr), new Vector2I(oq, or2));
        }

        public void add_anchor(string anchorId, string source, int q, int r, int duration, bool destructible = false, int hp = 1)
        {
            _currentCtx.Builder.AddBattleAnchor(anchorId, source, new Vector2I(q, r), duration, destructible, hp);
        }

        public void fail(string reason)
        {
            _currentCtx.Builder.Fail(reason);
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
            float extraCritChance = 0.0f;
            float targetAcMultiplier = 1.0f;

            if (opts != null)
            {
                if (opts["advantage"] is bool adv) advantage = adv;
                if (opts["disadvantage"] is bool dis) disadvantage = dis;
                if (opts["hit_mod"] is long hm) hitMod = (int)hm;
                else if (opts["hit_mod"] is double hmd) hitMod = (int)hmd;
                if (opts["damage_mult"] is double dm) damageMult = (float)dm;
                if (opts["node_flat_scale"] is double nfs) nodeFlatScale = (float)nfs;
                if (opts["critical_rate"] is double cr) extraCritChance = (float)cr;
                else if (opts["critical_rate"] is long crl) extraCritChance = crl;
                else if (opts["critical_rate"] is int cri) extraCritChance = cri;
                if (opts["target_ac_mult"] is double tam) targetAcMultiplier = (float)tam;
                else if (opts["target_ac_mult"] is long taml) targetAcMultiplier = taml;
                else if (opts["target_ac_mult"] is int tami) targetAcMultiplier = tami;
            }

            var attackerAllies = GetContextAlliesFor(attacker, exclude: attacker);
            var defenderAllies = GetContextAlliesFor(target, exclude: target);
            var godotResult = CombatResolver.ResolveAttack(
                attacker, target, _currentCtx.Grid,
                advantage, disadvantage, hitMod, damageMult,
                attackerAllies: attackerAllies,
                nodePassiveScale: nodeFlatScale,
                defenderAllies: defenderAllies,
                extraCritChance: extraCritChance,
                targetAcMultiplier: targetAcMultiplier,
                triggerVisuals: false);

            // 转换 Godot Dictionary → Lua table
            var table = GodotDictToLuaTable(godotResult);
            if (table == null) return null;
            table["__target_internal_id"] = targetProxy.InternalId;
            return table;
        }
    }

    private static Unit[] GetContextAlliesFor(Unit unit, Unit? exclude = null)
    {
        IEnumerable<Unit> units = unit.IsPlayerSide == _currentCtx.Attacker.IsPlayerSide
            ? _currentCtx.Allies
            : _currentCtx.Enemies;

        return units
            .Where(u => GodotObject.IsInstanceValid(u)
                && u != exclude
                && u.CurrentHp > 0
                && u.IsPlayerSide == unit.IsPlayerSide)
            .ToArray();
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

    private static Buff.BuffInstance CloneBuffTemplate(Buff.BuffInstance template)
    {
        return new Buff.BuffInstance
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            IconId = template.IconId,
            IsNegative = template.IsNegative,
            Tags = (string[])template.Tags.Clone(),
            Duration = template.Duration,
            MaxStacks = template.MaxStacks,
            Modifiers = template.Modifiers.Select(m => new Buff.StatModifier
            {
                Stat = m.Stat, Layer = m.Layer, Value = m.Value,
                Condition = m.Condition, Source = m.Source,
            }).ToList(),
            Triggers = template.Triggers.Select(t => new Buff.BuffTrigger
            {
                Event = t.Event, Effect = t.Effect, Condition = t.Condition,
                Chance = t.Chance, MaxTriggersPerCombat = t.MaxTriggersPerCombat,
            }).ToList(),
            OnTick = template.OnTick,
            Source = template.Source,
            CancelTags = (string[])template.CancelTags.Clone(),
            SaveToRemove = template.SaveToRemove,
            SaveDc = template.SaveDc,
            BreaksOnAttack = template.BreaksOnAttack,
            CanSpread = template.CanSpread,
            PersistOnDeath = template.PersistOnDeath,
        };
    }

    private static List<Buff.StatModifier> LuaTableToStatModifiers(LuaTable mods)
    {
        var result = new List<Buff.StatModifier>();
        foreach (var key in mods.Keys)
        {
            string stat = key.ToString()!;
            object? raw = mods[key];
            float value = raw switch
            {
                bool b => b ? 1f : 0f,
                long l => l,
                double d => (float)d,
                int i => i,
                float f => f,
                _ => 0f,
            };
            result.Add(new Buff.StatModifier { Stat = stat, Layer = InferModifierLayer(stat), Value = value });
        }
        return result;
    }

    private static Buff.ModifierLayer InferModifierLayer(string stat)
    {
        return stat switch
        {
            "damage" or "melee_damage" or "ranged_damage" => Buff.ModifierLayer.Increased,
            "damage_taken_final_mult" => Buff.ModifierLayer.FinalMult,
            "attack_advantage" => Buff.ModifierLayer.Override,
            _ => Buff.ModifierLayer.Base,
        };
    }
}
