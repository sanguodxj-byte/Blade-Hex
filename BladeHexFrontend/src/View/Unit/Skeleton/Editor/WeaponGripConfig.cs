// WeaponGripConfig.cs
// 武器握持配置 — 独立于动画数据，按武器类别存储
// 定义武器图片的握持点偏移和初始旋转角度
using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace BladeHex.View.Unit.Skeleton.Editor;

/// <summary>
/// 武器握持配置。
/// 每个武器动画类别有一套握持参数，所有动画共享。
/// </summary>
public sealed class WeaponGripConfig
{
    /// <summary>握持点 X 偏移（像素，正 = 右移图片）</summary>
    public float OffsetX { get; set; }

    /// <summary>握持点 Y 偏移（像素，正 = 上移图片）</summary>
    public float OffsetY { get; set; }

    /// <summary>图片初始旋转角度（度）</summary>
    public float Rotation { get; set; }

    /// <summary>对应的武器类别</summary>
    public WeaponAnimCategory Category { get; set; } = WeaponAnimCategory.Slash;

    // ═══════════════════════════════════════════
    // 默认预设
    // ═══════════════════════════════════════════

    public static WeaponGripConfig DefaultForCategory(WeaponAnimCategory cat) => cat switch
    {
        WeaponAnimCategory.Slash => new() { Category = cat, OffsetX = 0, OffsetY = -60, Rotation = -30 },
        WeaponAnimCategory.Thrust => new() { Category = cat, OffsetX = 0, OffsetY = -80, Rotation = -90 },
        WeaponAnimCategory.Crush => new() { Category = cat, OffsetX = 0, OffsetY = -50, Rotation = -20 },
        WeaponAnimCategory.Bow => new() { Category = cat, OffsetX = 0, OffsetY = -40, Rotation = 0 },
        WeaponAnimCategory.Crossbow => new() { Category = cat, OffsetX = 0, OffsetY = -30, Rotation = 0 },
        WeaponAnimCategory.Throw => new() { Category = cat, OffsetX = 0, OffsetY = -20, Rotation = -45 },
        WeaponAnimCategory.Catalyst => new() { Category = cat, OffsetX = 0, OffsetY = -50, Rotation = -10 },
        WeaponAnimCategory.Unarmed => new() { Category = cat, OffsetX = 0, OffsetY = 0, Rotation = 0 },
        _ => new() { Category = cat },
    };

    // ═══════════════════════════════════════════
    // 序列化
    // ═══════════════════════════════════════════

    private const string SaveDir = "user://custom_animations/weapon_config";

    /// <summary>保存握持配置</summary>
    public static void Save(WeaponGripConfig config)
    {
        DirAccess.MakeDirRecursiveAbsolute(SaveDir);
        string path = $"{SaveDir}/{config.Category.ToString().ToLower()}.json";
        var dto = new GripDto
        {
            category = config.Category.ToString().ToLower(),
            offset_x = config.OffsetX,
            offset_y = config.OffsetY,
            rotation = config.Rotation,
        };
        string json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
        GD.Print($"[WeaponGripConfig] 已保存: {path}");
    }

    /// <summary>加载握持配置（不存在则返回默认）</summary>
    public static WeaponGripConfig Load(WeaponAnimCategory category)
    {
        string path = $"{SaveDir}/{category.ToString().ToLower()}.json";
        if (!FileAccess.FileExists(path))
            return DefaultForCategory(category);

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return DefaultForCategory(category);

        try
        {
            var dto = JsonSerializer.Deserialize<GripDto>(file.GetAsText());
            if (dto == null) return DefaultForCategory(category);
            return new WeaponGripConfig
            {
                Category = category,
                OffsetX = dto.offset_x,
                OffsetY = dto.offset_y,
                Rotation = dto.rotation,
            };
        }
        catch
        {
            return DefaultForCategory(category);
        }
    }

    // 缓存
    private static readonly Dictionary<WeaponAnimCategory, WeaponGripConfig> _cache = new();

    /// <summary>获取握持配置（带缓存）</summary>
    public static WeaponGripConfig Get(WeaponAnimCategory category)
    {
        if (_cache.TryGetValue(category, out var cached)) return cached;
        var config = Load(category);
        _cache[category] = config;
        return config;
    }

    /// <summary>清除缓存</summary>
    public static void ClearCache() => _cache.Clear();

    // DTO
    private class GripDto
    {
        public string? category { get; set; }
        public float offset_x { get; set; }
        public float offset_y { get; set; }
        public float rotation { get; set; }
    }
}
