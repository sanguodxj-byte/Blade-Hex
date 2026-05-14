// RPGRuleEngine.cs
// RPG规则引擎 — 静态工具类，封装所有骰子、检定、等级、专精规则
// 对应策划案 02-RPG系统.md
using Godot;

namespace BladeHex.Data;

/// <summary>
/// RPG 规则引擎 — 静态工具类
/// 120级体系核心参数和计算公式
/// </summary>
public static class RPGRuleEngine
{
    // ========================================
    // 枚举
    // ========================================

    public enum SaveType
    {
        Fortitude, // 强韧豁免 — 基于CON
        Reflex,    // 反射豁免 — 基于DEX
        Will,      // 意志豁免 — 基于WIS
    }

    public enum DifficultyDC
    {
        VeryEasy = 5,
        Easy = 10,
        Medium = 15,
        Hard = 20,
        VeryHard = 25,
        Legendary = 30,
    }

    // ========================================
    // 常量 — 120级体系核心参数
    // ========================================

    public const int MaxLevel = 120;
    public const int BaseAttrTotal = 30;
    public const int AttrPerLevel = 1;
    public const int AttrMin = 5;
    public const int AttrMax = 40;

    /// <summary>六维属性键名列表</summary>
    public static readonly string[] AttrKeys = ["str", "dex", "con", "intel", "wis", "cha"];

    // ========================================
    // 经验值与等级表（120级）
    // 公式：每级升级所需XP = 300 + (level - 1) × 200
    // ========================================

    private static int[]? _xpTableCache;

    private static void EnsureXpTable()
    {
        if (_xpTableCache != null && _xpTableCache.Length >= MaxLevel) return;
        _xpTableCache = new int[MaxLevel];
        int cumulative = 0;
        _xpTableCache[0] = 0; // Level 1 = 0 XP
        for (int lv = 2; lv <= MaxLevel; lv++)
        {
            int needed = 300 + (lv - 2) * 200;
            cumulative += needed;
            _xpTableCache[lv - 1] = cumulative;
        }
    }

    /// <summary>获取120级累计经验值表</summary>
    public static int[] GetXpTable() { EnsureXpTable(); return _xpTableCache!; }

    /// <summary>获取指定等级的累计经验值</summary>
    public static int GetXpForLevel(int level)
    {
        if (level < 1 || level > MaxLevel) return 0;
        EnsureXpTable();
        return _xpTableCache![level - 1];
    }

    /// <summary>根据累计XP反查当前等级</summary>
    public static int GetLevelFromXp(int xp)
    {
        EnsureXpTable();
        for (int i = _xpTableCache!.Length - 1; i >= 0; i--)
            if (xp >= _xpTableCache[i]) return i + 1;
        return 1;
    }

    /// <summary>获取升到下一级所需的额外经验值</summary>
    public static int GetXpToNextLevel(int currentXp)
    {
        int currentLevel = GetLevelFromXp(currentXp);
        if (currentLevel >= MaxLevel) return 0;
        return GetXpForLevel(currentLevel + 1) - currentXp;
    }

    // ========================================
    // 专精加值表（适配120级）
    // ========================================

    /// <summary>
    /// 专精加值 = floor(sqrt(level)) + 1
    /// 比纯 sqrt 更宽裕，弥补去除属性修正后的命中压力
    /// 示例: Lv.1=+2, Lv.4=+3, Lv.9=+4, Lv.16=+5, Lv.25=+6, Lv.49=+8
    /// </summary>
    public static int GetProficiencyBonus(int level) =>
        (int)Mathf.Floor(Mathf.Sqrt(Mathf.Max(1, level))) + 1;

    // ========================================
    // 属性修正公式 (全系统统一使用)
    // StatMod = floor(sqrt(score / 2))
    // ========================================

    public static int GetStatModifier(int score) =>
        (int)Mathf.Floor(Mathf.Sqrt(score / 2.0f));

    // ========================================
    // 属性点数系统
    // ========================================

    /// <summary>计算指定等级的总属性点数（基础+升级）</summary>
    public static int GetTotalAttrPoints(int level) =>
        BaseAttrTotal + (Mathf.Max(1, level) - 1) * AttrPerLevel;

    /// <summary>计算当前属性总值</summary>
    public static int GetAttrsSum(Godot.Collections.Dictionary attrs)
    {
        int total = 0;
        foreach (string key in AttrKeys)
            total += attrs.ContainsKey(key) ? (int)attrs[key] : 0;
        return total;
    }

    /// <summary>计算剩余未分配点数</summary>
    public static int GetUnspentPoints(Godot.Collections.Dictionary attrs, int level) =>
        GetTotalAttrPoints(level) - GetAttrsSum(attrs);

    /// <summary>检查属性是否合法（总值不超，单项在范围内）</summary>
    public static bool IsAttrsValid(Godot.Collections.Dictionary attrs, int level)
    {
        if (GetUnspentPoints(attrs, level) < 0) return false;
        foreach (string key in AttrKeys)
        {
            int val = attrs.ContainsKey(key) ? (int)attrs[key] : 0;
            if (val < AttrMin) return false; // 属性不再设有上限
        }
        return true;
    }

    /// <summary>创建均匀初始属性分配</summary>
    public static Godot.Collections.Dictionary CreateDefaultAttrs(int level)
    {
        int total = GetTotalAttrPoints(level);
        int @base = total / 6;
        int remainder = total % 6;
        var attrs = new Godot.Collections.Dictionary();
        for (int i = 0; i < 6; i++)
            attrs[AttrKeys[i]] = @base + (i < remainder ? 1 : 0);
        return attrs;
    }

    // ========================================
    // 等级 → CR 映射
    // CR = floor(level / 6)
    // ========================================

    public static float GetCrFromLevel(int level)
    {
        if (level <= 0) return 0.0f;
        return Mathf.Floor(level / 6.0f);
    }

    public static int GetLevelFromCr(float cr) => Mathf.Max(1, (int)(cr * 6));

    // ========================================
    // HP 计算公式
    // HP = 基础HP + CON修正 × 等级
    // ========================================

    public static int CalculateMaxHp(int baseHp, int conScore, int level)
    {
        int conMod = GetStatModifier(conScore);
        return Mathf.Max(1, baseHp + conMod * level);
    }

    // ========================================
    // AP 计算 (Action Points)
    // AP = 基础AP + DEX修正 + (CON修正 / 2)
    // ========================================

    public static int CalculateMaxAp(int baseAp, int dexScore, int conScore)
    {
        int dexMod = GetStatModifier(dexScore);
        int conMod = GetStatModifier(conScore);
        return Mathf.Max(1, baseAp + dexMod + (int)Mathf.Floor(conMod / 2.0f));
    }

    public static int GetBaseApCost(WeaponData.WeaponSubtype subtype)
    {
        return WeaponRegistry.GetConfig(subtype).BaseApCost;
    }

    // ========================================
    // AC 与 生命值计算 (基于护甲 DR)

    /// <summary>
    /// 计算敏捷 AC 加成（受护甲最大敏捷限制）
    /// 公式: DEX修正值
    /// </summary>
    public static int CalculateDexAc(int dexScore) => GetStatModifier(dexScore);

    /// <summary>
    /// 计算综合 AC (包含属性加成与护甲 DR 的根号收益)
    /// 公式: BaseAC + DexMod + sqrt(ArmorDR)
    /// </summary>
    public static int CalculateAc(int baseAc, int dex, int armorDr)
    {
        int dexAc = CalculateDexAc(dex);
        int drAc = (int)Mathf.Floor(Mathf.Sqrt(armorDr));
        return baseAc + dexAc + drAc;
    }

    /// <summary>
    /// 计算综合 MaxHP (包含护甲 DR 提供的额外生命值)
    /// 公式: BaseHP + (ArmorDR * 10)
    /// </summary>
    public static int CalculateMaxHp(int baseHP, int armorDr)
    {
        return baseHP + (armorDr * 10);
    }

    // ========================================
    // d20 命中率体系
    // ========================================

    /// <summary>计算命中率百分比 (0.0~1.0)</summary>
    public static float CalculateHitChance(int attackBonus, int targetAc, bool hasAdvantage, bool hasDisadvantage)
    {
        if (hasAdvantage && hasDisadvantage) { hasAdvantage = false; hasDisadvantage = false; }
        int needed = targetAc - attackBonus;
        float normalChance = Mathf.Clamp((21.0f - needed) / 20.0f, 0.0f, 1.0f);
        if (hasAdvantage) return 1.0f - (1.0f - normalChance) * (1.0f - normalChance);
        if (hasDisadvantage) return normalChance * normalChance;
        return normalChance;
    }

    // ========================================
    // 骰子
    // ========================================

    public static int RollD20() => GD.RandRange(1, 20);

    public static int RollDice(int count, int sides)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
            total += GD.RandRange(1, sides);
        return total;
    }

    /// <summary>掷 Nd20（伤害专用），返回 {rolls, total, count}</summary>
    public static Godot.Collections.Dictionary RollNd20(int count)
    {
        count = Mathf.Max(1, count);
        var rolls = new Godot.Collections.Array();
        int total = 0;
        for (int i = 0; i < count; i++)
        {
            int r = GD.RandRange(1, 20);
            rolls.Add(r);
            total += r;
        }
        return new Godot.Collections.Dictionary
        {
            { "rolls", rolls }, { "total", total }, { "count", count },
        };
    }

    /// <summary>根据等级获取伤害骰子数 (1~20=1d20, 21~40=2d20, ..., 101~120=6d20)</summary>
    public static int GetDamageDiceCount(int level)
    {
        if (level <= 0) return 1;
        return Mathf.Min(6, 1 + (level - 1) / 20);
    }

    /// <summary>优势掷骰（掷两次取较高）</summary>
    public static Godot.Collections.Dictionary RollWithAdvantage()
    {
        int r1 = GD.RandRange(1, 20);
        int r2 = GD.RandRange(1, 20);
        return new Godot.Collections.Dictionary
        {
            { "result", Mathf.Max(r1, r2) }, { "roll1", r1 }, { "roll2", r2 },
        };
    }

    /// <summary>劣势掷骰（掷两次取较低）</summary>
    public static Godot.Collections.Dictionary RollWithDisadvantage()
    {
        int r1 = GD.RandRange(1, 20);
        int r2 = GD.RandRange(1, 20);
        return new Godot.Collections.Dictionary
        {
            { "result", Mathf.Min(r1, r2) }, { "roll1", r1 }, { "roll2", r2 },
        };
    }

    // ========================================
    // 豁免检定
    // ========================================

    /// <summary>
    /// 执行一次豁免检定
    /// 返回 {success, roll, modifier, total, dc}
    /// </summary>
    public static Godot.Collections.Dictionary MakeSave(
        int abilityScore, int proficiencyBonus, bool isProficient, int dc,
        bool hasAdvantage = false, bool hasDisadvantage = false)
    {
        if (hasAdvantage && hasDisadvantage) { hasAdvantage = false; hasDisadvantage = false; }

        int modifier = GetStatModifier(abilityScore);
        if (isProficient) modifier += proficiencyBonus;

        int roll;
        if (hasAdvantage) roll = (int)RollWithAdvantage()["result"];
        else if (hasDisadvantage) roll = (int)RollWithDisadvantage()["result"];
        else roll = RollD20();

        int total = roll + modifier;
        return new Godot.Collections.Dictionary
        {
            { "success", total >= dc }, { "roll", roll },
            { "modifier", modifier }, { "total", total }, { "dc", dc },
        };
    }

    /// <summary>获取豁免类型对应的属性键名</summary>
    public static string GetSaveAbility(SaveType saveType) => saveType switch
    {
        SaveType.Fortitude => "con",
        SaveType.Reflex => "dex",
        SaveType.Will => "wis",
        _ => "con",
    };

    // ========================================
    // 法术DC计算
    // DC = 8 + 施法属性修正 + 专精加值
    // ========================================

    public static int CalculateSpellDc(int castingAbilityScore, int proficiencyBonus) =>
        8 + GetStatModifier(castingAbilityScore) + proficiencyBonus;

    // ========================================
    // 伤势惩罚
    // ========================================

    /// <summary>获取伤势检定惩罚（基于当前HP百分比）</summary>
    public static Godot.Collections.Dictionary GetWoundPenalty(float hpPercent)
    {
        if (hpPercent >= 0.5f) return new() { { "all_checks", 0 }, { "name", "健康" } };
        if (hpPercent >= 0.25f) return new() { { "all_checks", -1 }, { "name", "轻伤" } };
        if (hpPercent > 0.0f) return new() { { "all_checks", -2 }, { "name", "重伤" } };
        return new() { { "all_checks", 0 }, { "name", "濒死" } };
    }

    // ========================================
    // DC 描述
    // ========================================

    public static string GetDcDescription(int dc)
    {
        if (dc <= 5) return "轻而易举";
        if (dc <= 10) return "应该没问题";
        if (dc <= 15) return "有把握";
        if (dc <= 20) return "有些冒险";
        if (dc <= 25) return "希望渺茫";
        return "近乎不可能";
    }
}
