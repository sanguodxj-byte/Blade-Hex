// LuaCombatAPI.cs
// 战斗相关 C# 函数注册到 Lua 全局表
//
// NLua 方式：通过 RegisterFunction 或直接赋值 C# 对象到 Lua 全局变量
// Lua 脚本通过 combat.roll_dice(2, 6) 等方式调用
using System;
using NLua;
using BladeHex.Data;
using BladeHex.Map;
using Godot;

namespace BladeHex.Scripting;

/// <summary>
/// 将纯逻辑层的战斗 API 注册到 Lua 全局表。
/// NLua 允许直接将 C# 对象暴露给 Lua，Lua 可以调用其公共方法。
/// </summary>
public static class LuaCombatAPI
{
    /// <summary>将所有 Core 层 API 注册到 Lua 实例</summary>
    public static void Register(Lua lua)
    {
        // 注册 API 对象到 Lua 全局变量
        lua["combat"] = new CombatApiObject();
        lua["hex"] = new HexApiObject();
    }
}

/// <summary>combat 全局表 — Lua 中通过 combat.roll_dice(2,6) 调用</summary>
public class CombatApiObject
{
    public int roll_dice(int count, int sides) => RPGRuleEngine.RollDice(count, sides);
    public int get_stat_mod(int statValue) => RPGRuleEngine.GetStatModifier(statValue);
    public int get_proficiency(int level) => 0;
}

/// <summary>hex 全局表 — Lua 中通过 hex.distance(0,0,1,0) 调用</summary>
public class HexApiObject
{
    /// <summary>获取六邻格坐标，返回 LuaTable 数组</summary>
    public Vector2I[] neighbors(int q, int r) => HexUtils.GetNeighbors(q, r);

    public int distance(int q1, int r1, int q2, int r2) => HexUtils.Distance(q1, r1, q2, r2);

    public Vector2I get_neighbor(int q, int r, int direction) => HexUtils.GetNeighbor(q, r, direction);
}
