// LuaScriptEngine.cs
// Lua 脚本引擎 — NLua VM 生命周期管理、脚本加载/缓存/热重载
//
// 设计原则：
//   - 单 VM 实例，所有技能共享（回合制无并发）
//   - 沙箱模式：移除 os/io/debug 库
//   - 脚本缓存：每个 skillId 只加载一次
//   - 错误不崩溃：捕获所有 Lua 异常，返回格式化错误信息
using System;
using System.Collections.Generic;
using NLua;
using Godot;

namespace BladeHex.Scripting;

/// <summary>
/// Lua 脚本引擎 — 管理 NLua VM、脚本加载与缓存。
/// 线程安全由调用方保证（回合制战斗无并发调用）。
/// </summary>
public sealed class LuaScriptEngine : IDisposable
{
    // ========================================================================
    // 单例
    // ========================================================================

    private static LuaScriptEngine? _instance;
    public static LuaScriptEngine Instance => _instance ??= new LuaScriptEngine();

    // ========================================================================
    // 内部状态
    // ========================================================================

    private readonly Lua _lua;
    private readonly HashSet<string> _loadedScripts = new();
    private bool _libLoaded;

    private const string SkillScriptPath = "res://scripts/skills/";
    private const string ModSkillPath = "user://mods/skills/";
    private const string LibFileName = "_lib.lua";

    // ========================================================================
    // 构造
    // ========================================================================

    private LuaScriptEngine()
    {
        _lua = new Lua();
        _lua.State.Encoding = System.Text.Encoding.UTF8;

        // 沙箱：移除危险库
        _lua.DoString(@"
            os = nil
            io = nil
            debug = nil
            loadfile = nil
            dofile = nil
            package = nil
        ");

        // 重定向 print 到 Godot 日志
        _lua.RegisterFunction("print", this, GetType().GetMethod(nameof(LuaPrint))!);
    }

    /// <summary>Lua print 重定向</summary>
    public void LuaPrint(params object[] args)
    {
        string msg = string.Join("\t", args ?? Array.Empty<object>());
        GD.Print($"[Lua] {msg}");
    }

    // ========================================================================
    // 公共 API
    // ========================================================================

    /// <summary>获取底层 NLua 实例（供 API 注册用）</summary>
    public Lua Lua => _lua;

    /// <summary>
    /// 执行指定技能的 Lua 脚本中的 execute(ctx) 函数。
    /// 返回 (success, errorMessage)。
    /// </summary>
    public (bool success, string? error) ExecuteSkill(string skillId, LuaTable ctxTable)
    {
        try
        {
            EnsureLibLoaded();
            EnsureSkillLoaded(skillId);

            // 调用全局 execute 函数（每个脚本加载后会覆盖全局 execute）
            // 为避免冲突，我们用 skill_<id>_execute 命名
            var func = _lua[$"_skill_{skillId}"] as LuaFunction;
            if (func == null)
                return (false, $"Lua skill function not found: _skill_{skillId}");

            func.Call(ctxTable);
            return (true, null);
        }
        catch (NLua.Exceptions.LuaScriptException ex)
        {
            string error = $"[Lua Error] {skillId}: {ex.Message}";
            GD.PushError(error);
            return (false, error);
        }
        catch (Exception ex)
        {
            string error = $"[Lua Error] {skillId}: {ex.Message}";
            GD.PushError(error);
            return (false, error);
        }
    }

    /// <summary>执行任意 Lua 代码片段（DebugConsole 用）</summary>
    public (bool success, string result) Execute(string code)
    {
        try
        {
            var results = _lua.DoString(code);
            if (results == null || results.Length == 0)
                return (true, "nil");
            return (true, results[0]?.ToString() ?? "nil");
        }
        catch (NLua.Exceptions.LuaScriptException ex)
        {
            return (false, $"Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <summary>重载单个技能脚本</summary>
    public bool Reload(string skillId)
    {
        _loadedScripts.Remove(skillId);
        try
        {
            EnsureSkillLoaded(skillId);
            GD.Print($"[LuaScriptEngine] Reloaded: {skillId}");
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"[LuaScriptEngine] Reload failed: {skillId} — {ex.Message}");
            return false;
        }
    }

    /// <summary>重载所有已缓存的技能脚本</summary>
    public int ReloadAll()
    {
        var ids = new List<string>(_loadedScripts);
        _loadedScripts.Clear();
        _libLoaded = false;

        EnsureLibLoaded();
        int count = 0;
        foreach (var id in ids)
        {
            try
            {
                EnsureSkillLoaded(id);
                count++;
            }
            catch { /* skip failed */ }
        }
        GD.Print($"[LuaScriptEngine] Reloaded all: {count}/{ids.Count} scripts");
        return count;
    }

    /// <summary>预加载 scripts/skills/ 目录下所有 .lua 文件</summary>
    public int PreloadAll()
    {
        EnsureLibLoaded();

        int count = 0;
        string dirPath = SkillScriptPath;

        if (!DirAccess.DirExistsAbsolute(dirPath))
        {
            GD.Print($"[LuaScriptEngine] Script directory not found: {dirPath}");
            return 0;
        }

        using var dir = DirAccess.Open(dirPath);
        if (dir == null) return 0;

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".lua") && fileName != LibFileName)
            {
                string skillId = fileName[..^4];
                try
                {
                    EnsureSkillLoaded(skillId);
                    count++;
                }
                catch (Exception ex)
                {
                    GD.PushError($"[LuaScriptEngine] Failed to preload {skillId}: {ex.Message}");
                }
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        GD.Print($"[LuaScriptEngine] Preloaded {count} skill scripts from {dirPath}");

        // 扫描 Mod 目录
        int modCount = PreloadDirectory(ModSkillPath);
        if (modCount > 0)
            GD.Print($"[LuaScriptEngine] Loaded {modCount} mod skill scripts from {ModSkillPath}");

        return count + modCount;
    }

    /// <summary>扫描指定目录加载所有 .lua 文件</summary>
    private int PreloadDirectory(string dirPath)
    {
        if (!DirAccess.DirExistsAbsolute(dirPath)) return 0;

        using var dir = DirAccess.Open(dirPath);
        if (dir == null) return 0;

        int count = 0;
        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".lua") && fileName != LibFileName)
            {
                string skillId = fileName[..^4];
                if (!_loadedScripts.Contains(skillId)) // Mod 不覆盖内置
                {
                    try
                    {
                        string source = ReadGodotFile(dirPath + fileName);
                        if (!string.IsNullOrEmpty(source))
                        {
                            string wrapped = $"do\n{source}\n_skill_{skillId} = execute\nexecute = nil\nend\n";
                            _lua.DoString(wrapped, fileName);
                            _loadedScripts.Add(skillId);
                            count++;
                            GD.Print($"[LuaScriptEngine] Loaded mod skill: {skillId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PushError($"[LuaScriptEngine] Failed to load mod {skillId}: {ex.Message}");
                    }
                }
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();
        return count;
    }

    /// <summary>检查指定技能是否有对应的 Lua 脚本文件（内置或 Mod）</summary>
    public bool HasScript(string skillId)
    {
        if (_loadedScripts.Contains(skillId)) return true;
        string path = SkillScriptPath + skillId + ".lua";
        if (FileAccess.FileExists(path)) return true;
        string modPath = ModSkillPath + skillId + ".lua";
        return FileAccess.FileExists(modPath);
    }

    /// <summary>创建一个新的 Lua table</summary>
    public LuaTable CreateTable()
    {
        _lua.NewTable("_tmp_tbl");
        var table = _lua["_tmp_tbl"] as LuaTable;
        _lua["_tmp_tbl"] = null;
        return table!;
    }

    // ========================================================================
    // 内部方法
    // ========================================================================

    private void EnsureLibLoaded()
    {
        if (_libLoaded) return;
        _libLoaded = true;

        string libPath = SkillScriptPath + LibFileName;
        if (!FileAccess.FileExists(libPath)) return;

        string source = ReadGodotFile(libPath);
        if (string.IsNullOrEmpty(source)) return;

        try
        {
            _lua.DoString(source, LibFileName);
            GD.Print("[LuaScriptEngine] Loaded _lib.lua");
        }
        catch (Exception ex)
        {
            GD.PushError($"[LuaScriptEngine] Failed to load _lib.lua: {ex.Message}");
        }
    }

    private void EnsureSkillLoaded(string skillId)
    {
        if (_loadedScripts.Contains(skillId)) return;

        // 优先内置，fallback 到 Mod 目录
        string path = SkillScriptPath + skillId + ".lua";
        if (!FileAccess.FileExists(path))
        {
            path = ModSkillPath + skillId + ".lua";
            if (!FileAccess.FileExists(path))
                throw new System.IO.FileNotFoundException($"Script not found: {skillId}.lua");
        }

        string source = ReadGodotFile(path);
        if (string.IsNullOrEmpty(source))
            throw new InvalidOperationException($"Cannot read: {path}");

        // 将脚本包装为命名函数，避免全局 execute 冲突
        // 原始脚本定义 function execute(ctx)，我们把它加载后赋值给 _skill_<id>
        string wrapped = $@"
do
    {source}
    _skill_{skillId} = execute
    execute = nil
end
";
        _lua.DoString(wrapped, skillId + ".lua");
        _loadedScripts.Add(skillId);
    }

    /// <summary>通过 Godot FileAccess 读取 res:// 路径的文本文件</summary>
    private static string ReadGodotFile(string resPath)
    {
        using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"[LuaScriptEngine] Cannot open: {resPath}");
            return "";
        }
        return file.GetAsText();
    }

    public void Dispose()
    {
        _lua.Dispose();
        if (_instance == this) _instance = null;
    }
}
