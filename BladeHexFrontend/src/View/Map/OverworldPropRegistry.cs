// OverworldPropRegistry.cs
// 大地图场景物体贴图注册表 — 按 prop_id 查找 Texture2D
// 贴图路径契约：res://src/assets/props/overworld/{propId}.png
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Map;

/// <summary>
/// 大地图 prop 贴图注册表。
/// 按 prop_id 加载并缓存贴图。找不到时返回占位贴图。
/// </summary>
public static class OverworldPropRegistry
{
    private const string PropsBaseDir = "res://src/assets/sprites/overworld_props";

    private static readonly Dictionary<string, Texture2D> _cache = new();
    private static Texture2D? _placeholder;

    /// <summary>按 prop_id 获取贴图；找不到时返回带颜色的占位贴图</summary>
    public static Texture2D GetTexture(string propId)
    {
        if (_cache.TryGetValue(propId, out var tex))
            return tex;

        string path = $"{PropsBaseDir}/{propId}.png";
        if (ResourceLoader.Exists(path))
        {
            var loaded = GD.Load<Texture2D>(path);
            if (loaded != null)
            {
                _cache[propId] = loaded;
                return loaded;
            }
        }

        // 无真实贴图 → 返回按类别着色的占位
        return GetColoredPlaceholder(propId);
    }

    /// <summary>prop 是否有真实贴图</summary>
    public static bool HasTexture(string propId)
    {
        if (_cache.ContainsKey(propId)) return true;
        return ResourceLoader.Exists($"{PropsBaseDir}/{propId}.png");
    }

    private static Texture2D GetPlaceholder()
    {
        if (_placeholder != null) return _placeholder;

        // 16x32 通用占位（不再使用，改用 GetColoredPlaceholder）
        var img = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);
        img.Fill(new Color(0.5f, 0.5f, 0.5f, 0.8f));
        _placeholder = ImageTexture.CreateFromImage(img);
        return _placeholder;
    }

    /// <summary>按 prop 类别生成不同颜色的占位贴图（用于调试分布）</summary>
    private static Texture2D GetColoredPlaceholder(string propId)
    {
        if (_colorCache.TryGetValue(propId, out var cached))
            return cached;

        Color color = GetDebugColor(propId);
        // 树类高而窄，岩石类矮而宽
        int w = IsTreeType(propId) ? 24 : 32;
        int h = IsTreeType(propId) ? 64 : 32;

        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(color);
        var tex = ImageTexture.CreateFromImage(img);
        _colorCache[propId] = tex;
        return tex;
    }

    private static Color GetDebugColor(string propId)
    {
        // 树木类 — 绿色系
        if (propId.Contains("oak")) return new Color(0.2f, 0.7f, 0.2f, 0.9f);
        if (propId.Contains("birch")) return new Color(0.5f, 0.8f, 0.3f, 0.9f);
        if (propId.Contains("pine") || propId.Contains("spruce")) return new Color(0.1f, 0.5f, 0.3f, 0.9f);
        if (propId.Contains("dark_oak")) return new Color(0.1f, 0.35f, 0.1f, 0.9f);
        if (propId.Contains("palm") || propId.Contains("jungle") || propId.Contains("vine")) return new Color(0.3f, 0.8f, 0.2f, 0.9f);
        if (propId.Contains("acacia")) return new Color(0.6f, 0.7f, 0.2f, 0.9f);
        if (propId.Contains("dead_tree")) return new Color(0.4f, 0.3f, 0.2f, 0.9f);
        if (propId.Contains("lone_tree")) return new Color(0.3f, 0.6f, 0.3f, 0.9f);
        if (propId.Contains("snow_pine")) return new Color(0.6f, 0.8f, 0.8f, 0.9f);

        // 灌木类 — 黄绿色系
        if (propId.Contains("bush") || propId.Contains("flower")) return new Color(0.6f, 0.75f, 0.2f, 0.9f);
        if (propId.Contains("reed")) return new Color(0.5f, 0.6f, 0.3f, 0.9f);
        if (propId.Contains("cactus")) return new Color(0.3f, 0.7f, 0.4f, 0.9f);

        // 岩石类 — 灰色系
        if (propId.Contains("rock") || propId.Contains("boulder")) return new Color(0.5f, 0.5f, 0.5f, 0.9f);
        if (propId.Contains("ice_rock") || propId.Contains("frozen")) return new Color(0.7f, 0.8f, 0.9f, 0.9f);
        if (propId.Contains("sand_rock")) return new Color(0.8f, 0.7f, 0.4f, 0.9f);
        if (propId.Contains("moss")) return new Color(0.4f, 0.55f, 0.35f, 0.9f);
        if (propId.Contains("cracked")) return new Color(0.6f, 0.45f, 0.3f, 0.9f);

        // 山脉类 — 深灰/棕色
        if (propId.Contains("mountain") || propId.Contains("peak")) return new Color(0.35f, 0.3f, 0.25f, 0.9f);
        if (propId.Contains("snow_peak")) return new Color(0.85f, 0.85f, 0.9f, 0.9f);
        if (propId.Contains("cliff")) return new Color(0.4f, 0.35f, 0.3f, 0.9f);

        // 其他 — 紫色（容易发现未分类的）
        if (propId.Contains("stump")) return new Color(0.5f, 0.35f, 0.2f, 0.9f);
        if (propId.Contains("bone")) return new Color(0.9f, 0.9f, 0.8f, 0.9f);
        if (propId.Contains("termite")) return new Color(0.7f, 0.5f, 0.3f, 0.9f);

        return new Color(0.8f, 0.2f, 0.8f, 0.9f); // 紫色 = 未识别
    }

    private static bool IsTreeType(string propId)
    {
        return propId.Contains("tree") || propId.Contains("oak") || propId.Contains("pine")
            || propId.Contains("spruce") || propId.Contains("palm") || propId.Contains("acacia")
            || propId.Contains("peak") || propId.Contains("cliff");
    }

    private static readonly Dictionary<string, Texture2D> _colorCache = new();

    /// <summary>清空缓存</summary>
    public static void ClearCache()
    {
        _cache.Clear();
        _placeholder = null;
    }
}
