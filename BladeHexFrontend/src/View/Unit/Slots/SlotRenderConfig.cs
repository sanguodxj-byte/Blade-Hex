// SlotRenderConfig.cs
// 装备分部位渲染配置 — View 层唯一真相源
// 迁移自 ItemData.SlotRenderConfig（Core），消除 Core 对 Vector3/Vector2 渲染坐标的依赖
using Godot;
using BladeHex.Data;

namespace BladeHex.View.Unit.Slots;

/// <summary>单个装备部位的渲染参数</summary>
[GlobalClass]
public partial class SlotRenderConfig : RefCounted
{
    /// <summary>部位枚举值</summary>
    [Export] public ItemData.EquipSlot Slot;
    /// <summary>相对于角色根节点的锚点偏移</summary>
    [Export] public Vector3 AnchorOffset { get; set; } = Vector3.Zero;
    /// <summary>渲染优先级（越大越靠前）</summary>
    [Export] public int ZOrder;
    /// <summary>默认贴图尺寸</summary>
    [Export] public Vector2 DefaultSize { get; set; } = new(64, 96);
    /// <summary>Sprite3D 像素大小</summary>
    [Export] public float PixelSize { get; set; } = 1.0f;
    /// <summary>z 轴额外偏移（避免 z-fighting）</summary>
    [Export] public float SortOffset;

    public int SlotIndex => (int)Slot;
}

/// <summary>
/// 部位渲染配置表 — 静态工具
/// </summary>
public static class SlotConfigTable
{
    /// <summary>所有部位的渲染配置表（按 z_order 排序）</summary>
    private static readonly SlotRenderConfig[] SlotConfigs =
    [
        new() { Slot = ItemData.EquipSlot.Body,    AnchorOffset = new Vector3(0, 0, 0),    ZOrder = 0, DefaultSize = new Vector2(64, 96),  PixelSize = 2.0f, SortOffset = 0.000f },
        new() { Slot = ItemData.EquipSlot.Costume, AnchorOffset = new Vector3(0, 4, 0),    ZOrder = 1, DefaultSize = new Vector2(72, 100), PixelSize = 2.0f, SortOffset = -0.10f },
        new() { Slot = ItemData.EquipSlot.Hands,   AnchorOffset = new Vector3(0, -20, 0),  ZOrder = 2, DefaultSize = new Vector2(48, 48),  PixelSize = 2.0f, SortOffset = -0.20f },
        new() { Slot = ItemData.EquipSlot.Head,    AnchorOffset = new Vector3(0, 96, 0),   ZOrder = 3, DefaultSize = new Vector2(48, 48),  PixelSize = 2.0f, SortOffset = -0.30f },
        new() { Slot = ItemData.EquipSlot.Helmet,  AnchorOffset = new Vector3(0, 104, 0),  ZOrder = 4, DefaultSize = new Vector2(56, 56),  PixelSize = 2.0f, SortOffset = -0.40f },
        new() { Slot = ItemData.EquipSlot.Weapon,  AnchorOffset = new Vector3(56, -10, 0), ZOrder = 5, DefaultSize = new Vector2(32, 80),  PixelSize = 2.0f, SortOffset = -0.50f },
    ];

    /// <summary>获取指定部位的渲染配置</summary>
    public static SlotRenderConfig GetSlotConfig(ItemData.EquipSlot slot)
        => SlotConfigs[(int)slot];

    /// <summary>获取所有部位渲染配置（按 z_order 排序）</summary>
    public static SlotRenderConfig[] GetAllSlotConfigs()
        => SlotConfigs;

    /// <summary>部位 → 可读名称</summary>
    public static string GetSlotName(ItemData.EquipSlot slot) => slot switch
    {
        ItemData.EquipSlot.Body => "身体",
        ItemData.EquipSlot.Costume => "服装",
        ItemData.EquipSlot.Hands => "手甲",
        ItemData.EquipSlot.Head => "头部",
        ItemData.EquipSlot.Helmet => "头盔",
        ItemData.EquipSlot.Weapon => "武器",
        _ => "未知",
    };

    /// <summary>部位是否允许换装（Body 层不可清除）</summary>
    public static bool IsSlotSwappable(ItemData.EquipSlot slot)
        => slot != ItemData.EquipSlot.Body;
}
