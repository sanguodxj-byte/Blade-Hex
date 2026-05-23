// EquipmentOffsetConfig.cs
// 装备部件偏移/缩放/旋转配置 — 独立于动画数据，按装备槽位存储
// 定义武器/护甲/头盔/手甲图片的偏移、缩放和旋转参数
using Godot;
using System.Collections.Generic;
using System.Text.Json;
using BladeHex.Data;

namespace BladeHex.View.Unit.Skeleton.Editor;

/// <summary>
/// 装备部件偏移/缩放/旋转配置。
/// 每个装备槽位有一套偏移参数，所有动画共享。
/// 支持的槽位：Weapon（武器）、Costume（护甲）、Helmet（头盔）、Hands（手甲）。
/// </summary>
public sealed class EquipmentOffsetConfig
{
    /// <summary>X 偏移（像素，正 = 右移图片）</summary>
    public float OffsetX { get; set; }

    /// <summary>Y 偏移（像素，正 = 上移图片）</summary>
    public float OffsetY { get; set; }

    /// <summary>缩放倍率（1.0 = 原始大小）</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>旋转角度（度，仅武器使用）</summary>
    public float Rotation { get; set; }

    /// <summary>水平翻转（镜像）</summary>
    public bool FlipH { get; set; }

    /// <summary>对应的装备槽位</summary>
    public ItemData.EquipSlot Slot { get; set; } = ItemData.EquipSlot.Costume;

    // ═══════════════════════════════════════════
    // 支持的槽位列表
    // ═══════════════════════════════════════════

    /// <summary>支持偏移编辑的槽位</summary>
    public static readonly ItemData.EquipSlot[] EditableSlots = new[]
    {
        ItemData.EquipSlot.Weapon,
        ItemData.EquipSlot.Costume,
        ItemData.EquipSlot.Helmet,
        ItemData.EquipSlot.Hands,
    };

    /// <summary>槽位显示名称</summary>
    public static string GetSlotDisplayName(ItemData.EquipSlot slot) => slot switch
    {
        ItemData.EquipSlot.Weapon => "武器",
        ItemData.EquipSlot.Costume => "护甲",
        ItemData.EquipSlot.Helmet => "头盔",
        ItemData.EquipSlot.Hands => "手甲",
        _ => slot.ToString(),
    };

    /// <summary>该槽位是否支持旋转编辑</summary>
    public static bool SupportsRotation(ItemData.EquipSlot slot) => slot == ItemData.EquipSlot.Weapon;

    // ═══════════════════════════════════════════
    // 默认预设
    // ═══════════════════════════════════════════

    public static EquipmentOffsetConfig DefaultForSlot(ItemData.EquipSlot slot) => new()
    {
        Slot = slot,
        OffsetX = 0,
        OffsetY = 0,
        Scale = 1.0f,
        Rotation = 0,
    };

    // ═══════════════════════════════════════════
    // 序列化
    // ═══════════════════════════════════════════

    private const string SaveDir = "user://custom_animations/equipment_offset";

    /// <summary>保存偏移配置</summary>
    public static void Save(EquipmentOffsetConfig config)
    {
        DirAccess.MakeDirRecursiveAbsolute(SaveDir);
        string path = $"{SaveDir}/{config.Slot.ToString().ToLower()}.json";
        var dto = new OffsetDto
        {
            slot = config.Slot.ToString().ToLower(),
            offset_x = config.OffsetX,
            offset_y = config.OffsetY,
            scale = config.Scale,
            rotation = config.Rotation,
            flip_h = config.FlipH,
        };
        string json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
        GD.Print($"[EquipmentOffsetConfig] 已保存: {path}");
    }

    /// <summary>加载偏移配置（不存在则尝试迁移旧格式，再回退到默认）</summary>
    public static EquipmentOffsetConfig Load(ItemData.EquipSlot slot)
    {
        string path = $"{SaveDir}/{slot.ToString().ToLower()}.json";
        if (!FileAccess.FileExists(path))
            return DefaultForSlot(slot);

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return DefaultForSlot(slot);

        try
        {
            var dto = JsonSerializer.Deserialize<OffsetDto>(file.GetAsText());
            if (dto == null) return DefaultForSlot(slot);
            return new EquipmentOffsetConfig
            {
                Slot = slot,
                OffsetX = dto.offset_x,
                OffsetY = dto.offset_y,
                Scale = dto.scale,
                Rotation = dto.rotation,
                FlipH = dto.flip_h,
            };
        }
        catch
        {
            return DefaultForSlot(slot);
        }
    }

    // ═══════════════════════════════════════════
    // 武器按类别+动画存储
    // ═══════════════════════════════════════════

    /// <summary>加载武器偏移配置（按武器类别+动画名）</summary>
    /// <remarks>
    /// 查找顺序：{category}_{animName}.json → {category}_idle.json → 默认值
    /// </remarks>
    public static EquipmentOffsetConfig LoadWeapon(WeaponAnimCategory category, string animName)
    {
        string dir = $"{SaveDir}/weapon";
        string catLower = category.ToString().ToLower();
        string path = $"{dir}/{catLower}_{animName}.json";

        if (!FileAccess.FileExists(path))
        {
            // Fallback: 尝试 idle
            if (animName != "idle")
            {
                string idlePath = $"{dir}/{catLower}_idle.json";
                if (FileAccess.FileExists(idlePath))
                    path = idlePath;
                else
                    return DefaultForSlot(ItemData.EquipSlot.Weapon);
            }
            else
            {
                return DefaultForSlot(ItemData.EquipSlot.Weapon);
            }
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return DefaultForSlot(ItemData.EquipSlot.Weapon);

        try
        {
            var dto = JsonSerializer.Deserialize<OffsetDto>(file.GetAsText());
            if (dto == null) return DefaultForSlot(ItemData.EquipSlot.Weapon);
            return new EquipmentOffsetConfig
            {
                Slot = ItemData.EquipSlot.Weapon,
                OffsetX = dto.offset_x,
                OffsetY = dto.offset_y,
                Scale = dto.scale,
                Rotation = dto.rotation,
                FlipH = dto.flip_h,
            };
        }
        catch
        {
            return DefaultForSlot(ItemData.EquipSlot.Weapon);
        }
    }

    /// <summary>保存武器偏移配置（按武器类别+动画名）</summary>
    public static void SaveWeapon(EquipmentOffsetConfig config, WeaponAnimCategory category, string animName)
    {
        string dir = $"{SaveDir}/weapon";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        string catLower = category.ToString().ToLower();
        string path = $"{dir}/{catLower}_{animName}.json";
        var dto = new OffsetDto
        {
            slot = "weapon",
            offset_x = config.OffsetX,
            offset_y = config.OffsetY,
            scale = config.Scale,
            rotation = config.Rotation,
            flip_h = config.FlipH,
        };
        string json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
        GD.Print($"[EquipmentOffsetConfig] 已保存武器偏移: {path}");
    }

    // ═══════════════════════════════════════════
    // 缓存（string-keyed，支持武器按类别+动画和其他槽位）
    // ═══════════════════════════════════════════

    private static readonly Dictionary<string, EquipmentOffsetConfig> _cache = new();

    /// <summary>获取偏移配置（带缓存，非武器槽位）</summary>
    public static EquipmentOffsetConfig Get(ItemData.EquipSlot slot)
    {
        string key = slot.ToString().ToLower();
        if (_cache.TryGetValue(key, out var cached)) return cached;
        var config = Load(slot);
        _cache[key] = config;
        return config;
    }

    /// <summary>获取武器偏移配置（带缓存，按类别+动画名）</summary>
    public static EquipmentOffsetConfig GetWeapon(WeaponAnimCategory category, string animName)
    {
        string key = $"weapon_{category.ToString().ToLower()}_{animName}";
        if (_cache.TryGetValue(key, out var cached)) return cached;
        var config = LoadWeapon(category, animName);
        _cache[key] = config;
        return config;
    }

    /// <summary>清除缓存</summary>
    public static void ClearCache() => _cache.Clear();

    // ═══════════════════════════════════════════
    // DTO
    // ═══════════════════════════════════════════

    private class OffsetDto
    {
        public string? slot { get; set; }
        public float offset_x { get; set; }
        public float offset_y { get; set; }
        public float scale { get; set; } = 1.0f;
        public float rotation { get; set; }
        public bool flip_h { get; set; }
    }
}
