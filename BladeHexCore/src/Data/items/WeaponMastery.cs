// WeaponMastery.cs
//
// 武器精通系统 — 按「伤害类型 × 轻重」共 9 种精通轨道
// 同类型同重量的所有变体共享同一精通进度（无视具体变体）
// v0.7: 精通等级 1-15，每级所需 XP 公差 50（升到 Lv.N 总 XP = (N-1)*N/2 * 50）
//   → 比角色等级升级更快，120 级角色应有约 Lv.13~14 主武器精通
//   → 命中加成 floor(Lv/2)，最高 +7；伤害加成 +10%/级，最高 +150%

using Godot;
using System.Collections.Generic;

namespace BladeHex.Data;

public class WeaponMastery
{
    // ========================================
    // 精通键 = 伤害类型 + 重量分级（共 9 种轨道）
    // e.g. (Slash, Heavy) = 所有重型砍伤武器共享
    // ========================================

    public readonly record struct MasteryKey(
        WeaponData.DamageType DamageType,
        WeaponData.WeightCategory Weight);

    // ========================================
    // 精通经验表（1-15 级，每级递增 50 XP）
    // 比角色等级升级更快：120 级角色已达 Lv.13~14 主武器精通
    // Lv1=0, Lv2=50, Lv3=150, Lv4=300, Lv5=500
    // Lv6=750, Lv7=1050, Lv8=1400, Lv9=1800, Lv10=2250
    // Lv11=2750, Lv12=3300, Lv13=3900, Lv14=4550, Lv15=5250
    // ========================================

    public const int MaxMasteryLevel = 15;

    /// <summary>升到第 level 级所需的总累计 XP（公差 50，二次曲线）</summary>
    public static int XpForLevel(int level)
    {
        if (level <= 1) return 0;
        level = Mathf.Min(level, MaxMasteryLevel);
        return (level - 1) * level / 2 * 50;
    }

    /// <summary>根据累计 XP 反查当前精通等级（1-15）</summary>
    public static int LevelFromXp(int xp)
    {
        for (int lv = MaxMasteryLevel; lv >= 1; lv--)
            if (xp >= XpForLevel(lv)) return lv;
        return 1;
    }

    /// <summary>距离下一级还需要多少 XP（满级返回 0）</summary>
    public static int XpToNextLevel(int currentXp)
    {
        int lv = LevelFromXp(currentXp);
        if (lv >= MaxMasteryLevel) return 0;
        return XpForLevel(lv + 1) - currentXp;
    }

    // ========================================
    // WeaponSubtype → MasteryKey 映射
    // 只看伤害类型 + 轻/中/重，无视具体变体名称
    // ========================================

    public static MasteryKey GetKeyFor(WeaponData.WeaponSubtype subtype)
    {
        return subtype switch
        {
            // ── 轻型砍伤 ──
            WeaponData.WeaponSubtype.Dagger or
            WeaponData.WeaponSubtype.Seax or
            WeaponData.WeaponSubtype.Kukri or
            WeaponData.WeaponSubtype.ThrowingKnife or
            WeaponData.WeaponSubtype.Francisca =>
                new(WeaponData.DamageType.Slash, WeaponData.WeightCategory.Light),

            // ── 中型砍伤 ──
            WeaponData.WeaponSubtype.ArmingSword or
            WeaponData.WeaponSubtype.BattleAxe or
            WeaponData.WeaponSubtype.NomadSaber =>
                new(WeaponData.DamageType.Slash, WeaponData.WeightCategory.Medium),

            // ── 重型砍伤 ──
            WeaponData.WeaponSubtype.Greatsword or
            WeaponData.WeaponSubtype.GreatAxe or
            WeaponData.WeaponSubtype.Glaive =>
                new(WeaponData.DamageType.Slash, WeaponData.WeightCategory.Heavy),

            // ── 轻型刺伤 ──
            WeaponData.WeaponSubtype.Stiletto or
            WeaponData.WeaponSubtype.SpikedDagger or
            WeaponData.WeaponSubtype.Rapier or
            WeaponData.WeaponSubtype.Dart or
            WeaponData.WeaponSubtype.Shortbow or
            WeaponData.WeaponSubtype.HuntingBow or
            WeaponData.WeaponSubtype.NomadBow or
            WeaponData.WeaponSubtype.LightCrossbow or
            WeaponData.WeaponSubtype.HuntingCrossbow or
            WeaponData.WeaponSubtype.PistolCrossbow =>
                new(WeaponData.DamageType.Pierce, WeaponData.WeightCategory.Light),

            // ── 中型刺伤 ──
            WeaponData.WeaponSubtype.InfantrySpear or
            WeaponData.WeaponSubtype.BroadSpear or
            WeaponData.WeaponSubtype.Awlpike or
            WeaponData.WeaponSubtype.Javelin or
            WeaponData.WeaponSubtype.Harpoon or
            WeaponData.WeaponSubtype.Strongbow or
            WeaponData.WeaponSubtype.RecurveBow or
            WeaponData.WeaponSubtype.WarBow or
            WeaponData.WeaponSubtype.StandardCrossbow or
            WeaponData.WeaponSubtype.StrongCrossbow or
            WeaponData.WeaponSubtype.SniperCrossbow =>
                new(WeaponData.DamageType.Pierce, WeaponData.WeightCategory.Medium),

            // ── 重型刺伤 ──
            WeaponData.WeaponSubtype.Lance or
            WeaponData.WeaponSubtype.Voulge or
            WeaponData.WeaponSubtype.Trident or
            WeaponData.WeaponSubtype.Pilum or
            WeaponData.WeaponSubtype.HeavyJavelin or
            WeaponData.WeaponSubtype.Longbow or
            WeaponData.WeaponSubtype.CompositeLongbow or
            WeaponData.WeaponSubtype.Greatbow or
            WeaponData.WeaponSubtype.HeavyCrossbow or
            WeaponData.WeaponSubtype.SiegeCrossbow or
            WeaponData.WeaponSubtype.Ballista =>
                new(WeaponData.DamageType.Pierce, WeaponData.WeightCategory.Heavy),

            // ── 轻型钝伤 ──
            WeaponData.WeaponSubtype.Club or
            WeaponData.WeaponSubtype.LightHammer or
            WeaponData.WeaponSubtype.Cestus or
            WeaponData.WeaponSubtype.StoneThrow =>
                new(WeaponData.DamageType.Crush, WeaponData.WeightCategory.Light),

            // ── 中型钝伤 ──
            WeaponData.WeaponSubtype.WingedMace or
            WeaponData.WeaponSubtype.MilitaryHammer or
            WeaponData.WeaponSubtype.Flail or
            WeaponData.WeaponSubtype.ThrowingHammer =>
                new(WeaponData.DamageType.Crush, WeaponData.WeightCategory.Medium),

            // ── 重型钝伤 ──
            WeaponData.WeaponSubtype.Maul or
            WeaponData.WeaponSubtype.Greatclub or
            WeaponData.WeaponSubtype.Polehammer =>
                new(WeaponData.DamageType.Crush, WeaponData.WeightCategory.Heavy),

            // 兜底：从 Registry 读取伤害类型，归入中型
            _ => new(WeaponRegistry.GetConfig(subtype).DamageType,
                     WeaponData.WeightCategory.Medium)
        };
    }

    // ========================================
    // 实例数据：9 条精通轨道的 XP 存储
    // ========================================

    private readonly Dictionary<MasteryKey, int> _xpMap = new();

    public int GetXp(MasteryKey key)
        => _xpMap.TryGetValue(key, out int xp) ? xp : 0;

    public int GetXpBySubtype(WeaponData.WeaponSubtype subtype)
        => GetXp(GetKeyFor(subtype));

    public void SetXp(MasteryKey key, int xp)
        => _xpMap[key] = xp;

    public void SetXpBySubtype(WeaponData.WeaponSubtype subtype, int xp)
        => SetXp(GetKeyFor(subtype), xp);

    public int GetLevel(MasteryKey key)
        => LevelFromXp(GetXp(key));

    public int GetLevelBySubtype(WeaponData.WeaponSubtype subtype)
        => GetLevel(GetKeyFor(subtype));

    /// <summary>精通等级对应的伤害倍率加成（每级 +10%，15 级最高 +150%）</summary>
    public float GetDamageBonus(WeaponData.WeaponSubtype subtype)
        => GetLevelBySubtype(subtype) * 0.1f;

    /// <summary>
    /// 造成伤害后调用，增加对应精通轨道的 XP
    /// 规则：造成多少点伤害 = 获得多少点 XP（与武器变体无关，只看类型+轻重）
    /// </summary>
    /// <returns>是否触发升级</returns>
    public bool AddDamageXp(WeaponData.WeaponSubtype subtype, int damage)
    {
        if (damage <= 0) return false;

        var key = GetKeyFor(subtype);
        int oldLevel = GetLevel(key);
        int capXp = XpForLevel(MaxMasteryLevel) + 999;
        _xpMap[key] = Mathf.Min(GetXp(key) + damage, capXp);

        int newLevel = GetLevel(key);
        if (newLevel > oldLevel)
        {
            GD.Print($"[精通升级] {key.DamageType}/{key.Weight} → Lv.{newLevel} (+{newLevel * 10}% 伤害)");
            return true;
        }
        return false;
    }
}
