// WeaponTraits.cs
// 武器特性 Flags enum — 取代分散的 bool 字段
//
// 优势：
//   - 单一字段统一存储所有特性，序列化为 int
//   - 用 HasFlag 查询：if (traits.HasFlag(WeaponTraits.Finesse))
//   - 加新特性 = 在 enum 加一行 + 在查询点处理，不需要新加 [Export] 字段
//   - JSON 中可以用数组表达：{"traits": ["finesse", "two_handed"]}
//
// 兼容性：
//   - WeaponData 仍提供 IsFinesse 等只读属性，从 Traits 派生
//   - 旧 JSON 字段（"finesse": true）仍能加载，自动设置对应 flag
using System;

namespace BladeHex.Data;

/// <summary>
/// 武器特性 — Flags enum，组合多个特性。
/// 加新特性：在此 enum 增加一项，更新对应处理逻辑。
/// </summary>
[Flags]
public enum WeaponTraits : long
{
    None = 0,

    // 持握方式
    /// <summary>双手持握</summary>
    TwoHanded = 1L << 0,
    /// <summary>可双持（轻巧武器）</summary>
    DualWieldable = 1L << 1,

    // 距离/射程
    /// <summary>远程武器（弓/弩等，使用箭筒弹药）</summary>
    Ranged = 1L << 2,
    /// <summary>投掷武器（标枪、飞刀等，自带弹药）</summary>
    Throwing = 1L << 3,
    /// <summary>长柄/触及（近战 +1 格范围）</summary>
    Reach = 1L << 4,

    // 属性使用
    /// <summary>灵巧（可用 DEX 替代 STR）</summary>
    Finesse = 1L << 5,

    // 特殊机制
    /// <summary>需要装填（十字弩）</summary>
    NeedsReload = 1L << 6,
    /// <summary>钝击（对亡灵全额伤害）</summary>
    Blunt = 1L << 7,
    /// <summary>破甲（命中检定时目标 AC -2）</summary>
    ArmorPiercing = 1L << 8,
    /// <summary>反骑兵（对冲锋目标伤害 ×2）</summary>
    AntiCavalry = 1L << 9,
    /// <summary>横扫（攻击多个相邻敌人时各 -2 命中）</summary>
    Sweep = 1L << 10,
    /// <summary>法术触媒（法杖/魔导书）</summary>
    Catalyst = 1L << 11,
    /// <summary>长弓（高 AP 消耗）</summary>
    Longbow = 1L << 12,
    /// <summary>十字弩（高 AP + 装填）</summary>
    Crossbow = 1L << 13,
}

/// <summary>WeaponTraits 解析与扩展方法</summary>
public static class WeaponTraitsExtensions
{
    /// <summary>JSON 字段 ID（snake_case） → Flag 映射</summary>
    private static readonly System.Collections.Generic.Dictionary<string, WeaponTraits> _byId = new()
    {
        ["two_handed"] = WeaponTraits.TwoHanded,
        ["dual_wield"] = WeaponTraits.DualWieldable,
        ["dual_wieldable"] = WeaponTraits.DualWieldable,
        ["ranged"] = WeaponTraits.Ranged,
        ["throwing"] = WeaponTraits.Throwing,
        ["reach"] = WeaponTraits.Reach,
        ["finesse"] = WeaponTraits.Finesse,
        ["needs_reload"] = WeaponTraits.NeedsReload,
        ["reload"] = WeaponTraits.NeedsReload,
        ["blunt"] = WeaponTraits.Blunt,
        ["armor_piercing"] = WeaponTraits.ArmorPiercing,
        ["anti_cavalry"] = WeaponTraits.AntiCavalry,
        ["sweep"] = WeaponTraits.Sweep,
        ["catalyst"] = WeaponTraits.Catalyst,
        ["longbow"] = WeaponTraits.Longbow,
        ["crossbow"] = WeaponTraits.Crossbow,
    };

    /// <summary>从 snake_case 字符串解析单个 trait</summary>
    public static WeaponTraits ParseId(string id)
        => _byId.TryGetValue(id, out var t) ? t : WeaponTraits.None;

    /// <summary>列出所有 trait 的 ID（供 schema/调试使用）</summary>
    public static System.Collections.Generic.IEnumerable<string> GetAllIds() => _byId.Keys;

    /// <summary>测试是否包含某个 trait（HasFlag 别名，可读性更好）</summary>
    public static bool Has(this WeaponTraits self, WeaponTraits trait) => (self & trait) == trait;

    /// <summary>添加 trait</summary>
    public static WeaponTraits With(this WeaponTraits self, WeaponTraits trait) => self | trait;

    /// <summary>移除 trait</summary>
    public static WeaponTraits Without(this WeaponTraits self, WeaponTraits trait) => self & ~trait;
}
