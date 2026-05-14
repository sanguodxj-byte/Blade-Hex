// BattlePropRegistry.cs
// 战斗地图立牌（tree/rock/building）贴图注册表
//
// 按 prop_id 查找 Texture2D；运行时加载并缓存。
// 贴图路径契约：res://src/assets/props/battle/{propId}.png
using Godot;
using System.Collections.Generic;

namespace BladeHex.View.Map;

public static class BattlePropRegistry
{
    private const string PropsBaseDir = "res://src/assets/props/battle";

    private static readonly Dictionary<string, Texture2D> _cache = new();
    private static Texture2D? _placeholder;

    /// <summary>按 prop_id 获取贴图；找不到时返回占位贴图（不返回 null）</summary>
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

        return GetPlaceholder();
    }

    /// <summary>prop 是否有真实贴图（美术资产齐全时返回 true）</summary>
    public static bool HasTexture(string propId)
    {
        if (_cache.ContainsKey(propId)) return true;
        return ResourceLoader.Exists($"{PropsBaseDir}/{propId}.png");
    }

    private static Texture2D GetPlaceholder()
    {
        if (_placeholder != null) return _placeholder;

        // 16x32 的紫色纸片占位，高而窄（类似树的比例）
        var img = Image.CreateEmpty(16, 32, false, Image.Format.Rgba8);
        img.Fill(new Color(0.8f, 0.2f, 0.8f, 1f));
        _placeholder = ImageTexture.CreateFromImage(img);
        return _placeholder;
    }

    /// <summary>清空缓存（主要用于测试）</summary>
    public static void ClearCache()
    {
        _cache.Clear();
        _placeholder = null;
    }
}
