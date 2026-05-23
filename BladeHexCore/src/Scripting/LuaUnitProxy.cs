// LuaUnitProxy.cs
// Unit (Node3D) 的轻量 Lua 代理 — 只暴露数据字段
//
// NLua 会自动将 C# 对象的公共属性/方法暴露给 Lua。
// Lua 中访问方式：proxy.hp, proxy.str, proxy:has_effect("poison")
using System;
using System.Collections.Generic;

namespace BladeHex.Scripting;

/// <summary>
/// Unit 的 Lua 代理。NLua 自动暴露公共属性和方法给 Lua。
/// Lua 中属性用 . 访问，方法用 : 调用。
/// </summary>
public class LuaUnitProxy
{
    // ========================================================================
    // 内部引用（实现细节，Lua 脚本不应直接使用）
    // ========================================================================

    public int InternalId { get; set; }
    public long instance_id { get; set; }
    public int character_id { get; set; }
    public Action<int, int>? OnHpChanged { get; set; }
    public Action<int, int>? OnManaChanged { get; set; }
    public Action<int, float>? OnApChanged { get; set; }
    public Action<int, int>? OnExtraActionsChanged { get; set; }

    // ========================================================================
    // 基础属性
    // ========================================================================

    public string name { get; set; } = "";
    public int level { get; set; }
    public bool is_enemy { get; set; }

    // 六维属性
    public int str { get; set; }
    public int dex { get; set; }
    public int con { get; set; }
    public int intel { get; set; }
    public int wis { get; set; }
    public int cha { get; set; }

    // 生命/魔力
    private int _hp;
    public int hp
    {
        get => _hp;
        set
        {
            int old = _hp;
            _hp = value;
            if (old != value) OnHpChanged?.Invoke(InternalId, value);
        }
    }

    public int max_hp { get; set; }

    private int _mana;
    public int mana
    {
        get => _mana;
        set
        {
            int old = _mana;
            _mana = value;
            if (old != value) OnManaChanged?.Invoke(InternalId, value);
        }
    }

    public int max_mana { get; set; }

    // 位置
    public int q { get; set; }
    public int r { get; set; }

    // 朝向
    public int facing { get; set; }

    // 战斗状态
    private float _ap;
    public float ap
    {
        get => _ap;
        set
        {
            float old = _ap;
            _ap = value;
            if (Math.Abs(old - value) > 0.001f) OnApChanged?.Invoke(InternalId, value);
        }
    }

    public int max_ap { get; set; }
    public int ac { get; set; }
    public int attack_bonus { get; set; }
    public int move_range { get; set; }

    // 运行时状态
    private int _extraActions;
    public int extra_actions
    {
        get => _extraActions;
        set
        {
            int old = _extraActions;
            _extraActions = value;
            if (old != value) OnExtraActionsChanged?.Invoke(InternalId, value);
        }
    }

    public int life_circle_used { get; set; }
    public int life_shield_used { get; set; }
    public int heroic_call_used { get; set; }

    // 敌人类型
    public string enemy_type { get; set; } = "";

    // ========================================================================
    // 状态效果查询
    // ========================================================================

    public HashSet<string> ActiveEffects { get; set; } = new();
    public HashSet<string> ActiveSkills { get; set; } = new();

    /// <summary>是否拥有指定状态效果（Lua: proxy:has_effect("poison")）</summary>
    public bool has_effect(string effectId) => ActiveEffects.Contains(effectId);

    /// <summary>是否拥有指定被动技能（Lua: proxy:has_skill("knowledge_power")）</summary>
    public bool has_skill(string skillId) => ActiveSkills.Contains(skillId);
}
