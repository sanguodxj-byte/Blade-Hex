// ResourceRegistry.cs
// View 层资源注册表 — id → Resource 查表
// 职责：Core 数据类只持 string IconId / SpriteFramesId / MaterialId，View 启动时扫描资源并做 id→Resource 映射
// 设计：
//   - 纯静态类（不作为 Node 挂到场景树）
//   - 懒加载 + 缓存：首次 Get 才 GD.Load，之后走字典
//   - 支持手动 Register（测试/热重载）
//   - Miss 时返回 null，不抛异常（调用方自行决定 fallback）
using Godot;
using System;
using System.Collections.Generic;

namespace BladeHex.View.Data;

/// <summary>
/// View 层资源注册表（线程不安全 —— Godot 主线程使用）
/// </summary>
public static class ResourceRegistry
{
    // id → 资源路径（manifest 扫描结果）
    private static readonly Dictionary<string, string> _iconPaths = new();
    private static readonly Dictionary<string, string> _spriteFramesPaths = new();
    private static readonly Dictionary<string, string> _materialPaths = new();

    // id → 已加载资源的缓存
    private static readonly Dictionary<string, Texture2D> _iconCache = new();
    private static readonly Dictionary<string, SpriteFrames> _spriteFramesCache = new();
    private static readonly Dictionary<string, Material> _materialCache = new();

    // 初始化状态（供 T-305 实装时替换）
    public static bool IsInitialized { get; private set; }

    // ========================================
    // 初始化
    // ========================================

    /// <summary>
    /// 骨架实装：扫描默认资源目录建立 id → 路径映射
    /// T-305 会扩展为支持 manifest JSON 与多目录扫描
    /// </summary>
    public static void Initialize()
    {
        if (IsInitialized) return;
        ScanIconDirectories();
        LoadManifest();
        IsInitialized = true;
        GD.Print($"[ResourceRegistry] Initialized: {_iconPaths.Count} icons, {_spriteFramesPaths.Count} sprite frames, {_materialPaths.Count} materials");
    }

    /// <summary>强制重扫（热重载）</summary>
    public static void Reload()
    {
        Clear();
        Initialize();
    }

    /// <summary>清空注册簿（测试用）</summary>
    public static void Clear()
    {
        _iconPaths.Clear();
        _spriteFramesPaths.Clear();
        _materialPaths.Clear();
        _iconCache.Clear();
        _spriteFramesCache.Clear();
        _materialCache.Clear();
        IsInitialized = false;
    }

    // ========================================
    // 手动注册（测试 / 动态资源）
    // ========================================

    public static void RegisterIcon(string id, string resPath)    => _iconPaths[id] = resPath;
    public static void RegisterIcon(string id, Texture2D texture) => _iconCache[id] = texture;

    public static void RegisterSpriteFrames(string id, string resPath)     => _spriteFramesPaths[id] = resPath;
    public static void RegisterSpriteFrames(string id, SpriteFrames frames) => _spriteFramesCache[id] = frames;

    public static void RegisterMaterial(string id, string resPath) => _materialPaths[id] = resPath;
    public static void RegisterMaterial(string id, Material mat)   => _materialCache[id] = mat;

    // ========================================
    // 查表
    // ========================================

    public static Texture2D? GetIcon(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_iconCache.TryGetValue(id, out var cached)) return cached;
        if (_iconPaths.TryGetValue(id, out var path))
        {
            var tex = GD.Load<Texture2D>(path);
            if (tex != null) _iconCache[id] = tex;
            return tex;
        }
        return null;
    }

    public static SpriteFrames? GetSpriteFrames(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_spriteFramesCache.TryGetValue(id, out var cached)) return cached;
        if (_spriteFramesPaths.TryGetValue(id, out var path))
        {
            var frames = GD.Load<SpriteFrames>(path);
            if (frames != null) _spriteFramesCache[id] = frames;
            return frames;
        }
        return null;
    }

    public static Material? GetMaterial(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_materialCache.TryGetValue(id, out var cached)) return cached;
        if (_materialPaths.TryGetValue(id, out var path))
        {
            var mat = GD.Load<Material>(path);
            if (mat != null) _materialCache[id] = mat;
            return mat;
        }
        return null;
    }

    /// <summary>泛型查表（按 T 类型自动分派到 icon/frames/material）</summary>
    public static bool TryGet<T>(string? id, out T? res) where T : Resource
    {
        res = null;
        if (string.IsNullOrEmpty(id)) return false;

        if (typeof(T) == typeof(Texture2D))
        {
            res = GetIcon(id) as T;
        }
        else if (typeof(T) == typeof(SpriteFrames))
        {
            res = GetSpriteFrames(id) as T;
        }
        else if (typeof(T) == typeof(Material))
        {
            res = GetMaterial(id) as T;
        }
        else
        {
            // 回退：分别查 3 张表里有没有匹配路径
            if (_iconPaths.TryGetValue(id, out var p1)) { res = GD.Load<T>(p1); }
            else if (_spriteFramesPaths.TryGetValue(id, out var p2)) { res = GD.Load<T>(p2); }
            else if (_materialPaths.TryGetValue(id, out var p3)) { res = GD.Load<T>(p3); }
        }
        return res != null;
    }

    // ========================================
    // Manifest 加载（T-305 实装）
    // ========================================

    private const string ManifestPath = "res://assets/resource_manifest.json";

    /// <summary>
    /// 从 resource_manifest.json 加载 id → 路径映射
    /// 如果文件不存在或解析失败，静默跳过（仅靠目录扫描兜底）
    /// </summary>
    private static void LoadManifest()
    {
        if (!Godot.FileAccess.FileExists(ManifestPath)) return;

        using var file = Godot.FileAccess.Open(ManifestPath, Godot.FileAccess.ModeFlags.Read);
        if (file == null) return;

        var json = file.GetAsText();
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var jsonParser = new Godot.Json();
            var parseError = jsonParser.Parse(json);
            if (parseError != Godot.Error.Ok) return;

            var data = jsonParser.Data;
            if (data.VariantType != Godot.Variant.Type.Dictionary) return;

            var manifest = data.AsGodotDictionary();
            int preCount = _iconPaths.Count;

            if (manifest.TryGetValue("icons", out var iconsVar) && iconsVar.VariantType == Godot.Variant.Type.Dictionary)
            {
                var icons = iconsVar.AsGodotDictionary();
                foreach (var key in icons.Keys)
                    _iconPaths[key.AsString()] = icons[key].AsString();
            }
            if (manifest.TryGetValue("sprite_frames", out var framesVar) && framesVar.VariantType == Godot.Variant.Type.Dictionary)
            {
                var frames = framesVar.AsGodotDictionary();
                foreach (var key in frames.Keys)
                    _spriteFramesPaths[key.AsString()] = frames[key].AsString();
            }
            if (manifest.TryGetValue("materials", out var matsVar) && matsVar.VariantType == Godot.Variant.Type.Dictionary)
            {
                var mats = matsVar.AsGodotDictionary();
                foreach (var key in mats.Keys)
                    _materialPaths[key.AsString()] = mats[key].AsString();
            }

            int added = _iconPaths.Count - preCount;
            GD.Print($"[ResourceRegistry] Manifest loaded: {added} new icons");
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[ResourceRegistry] Manifest parse error: {ex.Message}");
        }
    }

    // ========================================
    // 骨架扫描（T-305 实装）
    // ========================================

    private static readonly string[] DefaultIconDirs =
    {
        // 新资产目录（res://assets/generated_*）
        "res://assets/generated_weapons",
        "res://assets/generated_armor",
        "res://assets/generated_helmets",
        "res://assets/generated_shields",
        "res://assets/generated_staves",
        "res://assets/generated_spellbooks",
        "res://assets/generated_consumables",
        "res://assets/generated_class_icons",
        "res://assets/generated_skill_icons",
        "res://assets/generated_ui_icons",
        // 旧路径（兼容）
        "res://src/assets/generated/class_icons",
        "res://src/assets/generated/accessories",
        "res://src/assets/generated/armor",
        "res://src/assets/generated/consumables",
        "res://src/assets/tiles",
    };

    private static void ScanIconDirectories()
    {
        foreach (var dir in DefaultIconDirs)
        {
            TryScanDir(dir, _iconPaths, ".png", stripExt: true);
        }
    }

    private static void TryScanDir(string dirPath, Dictionary<string, string> target, string ext, bool stripExt)
    {
        using var dir = DirAccess.Open(dirPath);
        if (dir == null) return;
        dir.ListDirBegin();
        for (var file = dir.GetNext(); !string.IsNullOrEmpty(file); file = dir.GetNext())
        {
            if (dir.CurrentIsDir()) continue;
            if (!file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;
            var id = stripExt ? System.IO.Path.GetFileNameWithoutExtension(file) : file;
            var full = $"{dirPath}/{file}";
            target[id] = full;
        }
        dir.ListDirEnd();
    }
}
