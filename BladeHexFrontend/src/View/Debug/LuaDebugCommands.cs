// LuaDebugCommands.cs
// DebugConsole 的 Lua 相关命令注册
//
// 命令：
//   lua <code>       — 执行任意 Lua 代码片段
//   lua_reload <id>  — 重载指定技能脚本
//   lua_reload_all   — 重载所有技能脚本
//   lua_preload      — 预加载所有技能脚本
//   lua_test         — 执行简单 Lua 测试（验证引擎工作）
using Godot;
using BladeHex.Scripting;
using BladeHex.Combat;

namespace BladeHex.Debug;

/// <summary>
/// Lua 调试命令 — 在 DebugConsole 注册 Lua 相关命令。
/// </summary>
public static class LuaDebugCommands
{
    private static bool _registered;

    /// <summary>注册所有 Lua 调试命令到 DebugConsole</summary>
    public static void Register(Node consoleNode)
    {
        if (_registered) return;
        _registered = true;

        if (consoleNode is not DebugConsole console) return;

        console.RegisterCommand("lua", CmdLua, "lua <code> 执行 Lua 代码片段");
        console.RegisterCommand("lua_test", CmdLuaTest, "验证 Lua 引擎工作正常");
        console.RegisterCommand("lua_reload", CmdLuaReload, "lua_reload <skill_id> 重载技能脚本");
        console.RegisterCommand("lua_reload_all", CmdLuaReloadAll, "重载所有 Lua 技能脚本");
        console.RegisterCommand("lua_preload", CmdLuaPreload, "预加载所有技能脚本");

        GD.Print("[LuaDebugCommands] Registered Lua debug commands");
    }

    private static string? CmdLua(string[] args)
    {
        if (args.Length == 0) return "用法: lua <code>";
        string code = string.Join(" ", args);

        LuaSkillBridge.EnsureInitialized();
        var (success, result) = LuaScriptEngine.Instance.Execute(code);
        return success ? $"= {result}" : result;
    }

    private static string? CmdLuaTest(string[] args)
    {
        LuaSkillBridge.EnsureInitialized();
        var engine = LuaScriptEngine.Instance;

        // 测试 1: 基础运算
        var (ok1, r1) = engine.Execute("return 1 + 1");
        if (!ok1 || r1 != "2") return $"FAIL: 基础运算 (expected 2, got {r1})";

        // 测试 2: 字符串操作
        var (ok2, r2) = engine.Execute("return string.len('hello')");
        if (!ok2 || r2 != "5") return $"FAIL: 字符串 (expected 5, got {r2})";

        // 测试 3: combat API 可用
        var (ok3, r3) = engine.Execute("return combat:roll_dice(1, 1)");
        if (!ok3 || r3 != "1") return $"FAIL: combat.roll_dice (expected 1, got {r3})";

        // 测试 4: hex API 可用
        var (ok4, r4) = engine.Execute("return hex:distance(0, 0, 1, 0)");
        if (!ok4 || r4 != "1") return $"FAIL: hex.distance (expected 1, got {r4})";

        // 测试 5: 沙箱安全
        var (ok5, _) = engine.Execute("return os");
        // os 应该是 nil
        if (ok5)
        {
            var (_, osVal) = engine.Execute("return type(os)");
            if (osVal != "nil") return "FAIL: 沙箱泄漏 (os 应该为 nil)";
        }

        return "ALL PASS — Lua 引擎 (NLua/Lua 5.4) 工作正常 ✓";
    }

    private static string? CmdLuaReload(string[] args)
    {
        if (args.Length == 0) return "用法: lua_reload <skill_id>";
        string skillId = args[0];
        bool ok = LuaScriptEngine.Instance.Reload(skillId);
        return ok ? $"已重载: {skillId}" : $"重载失败: {skillId}（文件不存在或语法错误）";
    }

    private static string? CmdLuaReloadAll(string[] args)
    {
        int count = LuaScriptEngine.Instance.ReloadAll();
        return $"已重载 {count} 个脚本";
    }

    private static string? CmdLuaPreload(string[] args)
    {
        LuaSkillBridge.EnsureInitialized();
        int count = LuaScriptEngine.Instance.PreloadAll();
        return $"已预加载 {count} 个技能脚本";
    }
}
