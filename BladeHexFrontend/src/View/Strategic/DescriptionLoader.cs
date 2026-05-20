// DescriptionLoader.cs
// 从 encounter_descriptions.json 加载遭遇叙事文本
// 无硬编码回退 — JSON 加载失败时报错
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 遭遇描述加载器 — 从 res://BladeHexFrontend/src/View/Strategic/encounter_descriptions.json 加载
/// 加载失败时输出错误日志，不提供回退数据
/// </summary>
public static class DescriptionLoader
{
    private static bool _loaded = false;
    private static readonly Dictionary<string, string[]> _pools = new();

    /// <summary>获取指定类型的描述文本池</summary>
    public static string[]? GetPool(string key)
    {
        EnsureLoaded();
        return _pools.TryGetValue(key, out var pool) ? pool : null;
    }

    /// <summary>获取所有已加载的描述池</summary>
    public static Dictionary<string, string[]> GetAllPools()
    {
        EnsureLoaded();
        return _pools;
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        LoadFromJson();
    }

    private static void LoadFromJson()
    {
        string path = "res://BladeHexFrontend/src/View/Strategic/encounter_descriptions.json";
        if (!FileAccess.FileExists(path))
        {
            GD.PrintErr($"[DescriptionLoader] 描述文件不存在: {path}");
            return;
        }
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[DescriptionLoader] 无法打开描述文件: {path}");
            return;
        }
        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
        {
            GD.PrintErr($"[DescriptionLoader] JSON 解析错误: {json.GetErrorMessage()}");
            return;
        }
        var data = json.Data.AsGodotDictionary();
        ParsePools(data);
        GD.Print($"[DescriptionLoader] 已加载 {_pools.Count} 个描述池");
    }

    private static void ParsePools(Godot.Collections.Dictionary root)
    {
        foreach (var key in root.Keys)
        {
            string poolKey = key.AsString();
            var arr = root[key].AsGodotArray();
            var texts = new List<string>();
            foreach (var item in arr)
            {
                texts.Add(item.AsString());
            }
            _pools[poolKey] = texts.ToArray();
        }
    }
}
