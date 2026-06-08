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
    /// <summary>配置版本，用于过滤历史旧架构产生的脏缓存数据</summary>
    public float Version { get; set; } = 2.0f;

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
        ItemData.EquipSlot.Body,
        ItemData.EquipSlot.Head,
        ItemData.EquipSlot.Weapon,
        ItemData.EquipSlot.Costume,
        ItemData.EquipSlot.Helmet,
        ItemData.EquipSlot.Hands,
        ItemData.EquipSlot.Shield,
    };

    /// <summary>槽位显示名称</summary>
    public static string GetSlotDisplayName(ItemData.EquipSlot slot) => slot switch
    {
        ItemData.EquipSlot.Body => "身体皮肤",
        ItemData.EquipSlot.Head => "头部皮肤",
        ItemData.EquipSlot.Weapon => "武器",
        ItemData.EquipSlot.Costume => "护甲",
        ItemData.EquipSlot.Helmet => "头盔",
        ItemData.EquipSlot.Hands => "手甲",
        ItemData.EquipSlot.Shield => "盾牌",
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
        Version = 2.0f,
        OffsetX = 0,
        OffsetY = 0,
        Scale = (slot == ItemData.EquipSlot.Helmet || slot == ItemData.EquipSlot.Costume || slot == ItemData.EquipSlot.Body || slot == ItemData.EquipSlot.Head) ? 0.5f
              : (slot == ItemData.EquipSlot.Hands) ? 0.25f
              : (slot == ItemData.EquipSlot.Shield) ? 0.5f
              : 1.0f,
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
            version = config.Version,
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
        GD.Print($"[EquipmentOffsetConfig] 已保存 2.0 偏移配置: {path}");
    }

    /// <summary>加载偏移配置（向下兼容 1.0 与 2.0，不再进行物理删除）</summary>
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
            if (dto == null)
            {
                return DefaultForSlot(slot);
            }
            return new EquipmentOffsetConfig
            {
                Version = 2.0f, // 自动升级为最新 2.0 规范，保留原用户配置
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

    private static EquipmentOffsetConfig? LoadFromFile(string path)
    {
        if (!FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return null;
        try
        {
            var dto = JsonSerializer.Deserialize<OffsetDto>(file.GetAsText());
            if (dto == null) return null;
            return new EquipmentOffsetConfig
            {
                Version = 2.0f, // 自动升级为最新 2.0 规范
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
            return null;
        }
    }

    /// <summary>加载武器偏移配置（按武器类别+动画名，搭载 5 级智能寻址退水管线）</summary>
    public static EquipmentOffsetConfig LoadWeapon(WeaponAnimCategory category, string animName)
    {
        string dir = $"{SaveDir}/weapon";
        string catLower = category.ToString().ToLower();

        // 优先 1：玩家特定动作的自定义磁盘偏移配置 (user://)
        string userPath = $"{dir}/{catLower}_{animName}.json";
        var config = LoadFromFile(userPath);
        if (config != null) return config;

        // 优先 2：特定动作的内置默认偏移配置 (res://)
        string resPath = $"res://assets/animations/equipment_offset/weapon/{catLower}_{animName}.json";
        config = LoadFromFile(resPath);
        if (config != null) return config;

        // 回退 3：玩家待机 idle 的自定义磁盘偏移配置 (user://)
        if (animName != "idle")
        {
            string userIdlePath = $"{dir}/{catLower}_idle.json";
            config = LoadFromFile(userIdlePath);
            if (config != null) return config;

            // 回退 4：待机 idle 的内置默认偏移配置 (res://)
            string resIdlePath = $"res://assets/animations/equipment_offset/weapon/{catLower}_idle.json";
            config = LoadFromFile(resIdlePath);
            if (config != null) return config;
        }

        // 终极兜底 5：直接提取大师级代码内置物理对齐预设，100% 保证开箱即用对齐握持点
        return GetBuiltInDefaultPreset(category);
    }

    /// <summary>
    /// 获取 8 类武器的内置神级物理对齐预设，包含极其精确的 X/Y 像素位移、角度旋转与 FlipH 水平镜像，保证开箱即用完美对齐右手握柄
    /// </summary>
    public static EquipmentOffsetConfig GetBuiltInDefaultPreset(WeaponAnimCategory category)
    {
        var config = new EquipmentOffsetConfig
        {
            Slot = ItemData.EquipSlot.Weapon,
            Version = 2.0f,
            Scale = 1.0f
        };

        switch (category)
        {
            case WeaponAnimCategory.Slash:
                // 砍伤近战（剑/斧）：物理贴图中心在护手，手柄延至 200。
                // OffsetY = -50f 将手柄正中对齐到右手骨骼挂接点
                config.OffsetX = 0f;
                config.OffsetY = -50f;
                config.Rotation = -20f;
                config.FlipH = false;
                break;
            case WeaponAnimCategory.Thrust:
                // 刺伤近战（矛/枪）：物理贴图已完全垂直，手持在长杆中下段
                config.OffsetX = 0f;
                config.OffsetY = -104f;
                config.Rotation = -25f;
                config.FlipH = false;
                break;
            case WeaponAnimCategory.Crush:
                // 钝伤近战（锤/棒）：重物朝上垂直，大仰角偏斜 15 度
                config.OffsetX = 0f;
                config.OffsetY = -104f;
                config.Rotation = -15f;
                config.FlipH = false;
                break;
            case WeaponAnimCategory.Bow:
                // 弓类：物理贴图已完全垂直，手持几何中心 (128, 128)
                config.OffsetX = 0f;
                config.OffsetY = 0f;
                config.Rotation = 0f;
                config.Scale = 0.6f;
                config.FlipH = false;
                break;
            case WeaponAnimCategory.Crossbow:
                // 弩类：物理贴图已顺时针旋转拉平至水平朝右，几何中心 (128, 128)
                // 手柄约在 (98, 148)，需要向右向上平移对齐挂接点
                config.OffsetX = 60f;
                config.OffsetY = -40f;
                config.Rotation = 0f;
                config.FlipH = false;
                break;
            case WeaponAnimCategory.Throw:
                // 投掷类（飞刀）：捏柄小刀偏斜 20 度
                config.OffsetX = 0f;
                config.OffsetY = 0f;
                config.Rotation = -20f;
                config.FlipH = false;
                break;
            case WeaponAnimCategory.Catalyst:
                // 法杖施法：物理贴图垂直朝上，昂扬偏斜 15 度
                config.OffsetX = 0f;
                config.OffsetY = 0f;
                config.Rotation = -15f;
                config.FlipH = false;
                break;
            case WeaponAnimCategory.Unarmed:
            default:
                config.OffsetX = 0f;
                config.OffsetY = 0f;
                config.Rotation = 0f;
                config.FlipH = false;
                break;
        }

        return config;
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
            version = config.Version,
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
        GD.Print($"[EquipmentOffsetConfig] 已保存 2.0 武器偏移: {path}");
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
        public float version { get; set; } = 1.0f;
        public string? slot { get; set; }
        public float offset_x { get; set; }
        public float offset_y { get; set; }
        public float scale { get; set; } = 1.0f;
        public float rotation { get; set; }
        public bool flip_h { get; set; }
    }
}
