// CareerSkillRegistryV1Tests.cs
// v1 职业技能注册表回归测试 — 验证全部 63 个技能注册正确
//
// 每项检查验证底层 C# 数据结构状态，不依赖 Godot 场景树。
// 使用方法与 CombatRuleEngineTests 等相同，通过 UnitTestRunner 运行。
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using BladeHex.Strategic;

namespace BladeHex.Tests.SkillTree;

public static class CareerSkillRegistryV1Tests
{
    public static (int passed, int failed, List<string> details) RunAll()
    {
        var details = new List<string>();
        int passed = 0, failed = 0;

        foreach (var (name, ok, msg) in EnumerateTests())
        {
            if (ok) { passed++; details.Add($"  [PASS] {name}"); }
            else { failed++; details.Add($"  [FAIL] {name}: {msg}"); }
        }
        return (passed, failed, details);
    }

    private static IEnumerable<(string name, bool ok, string msg)> EnumerateTests()
    {
        yield return Run(nameof(AllSkills_Count_63), AllSkills_Count_63);
        yield return Run(nameof(AllSkills_EffectIds_Unique), AllSkills_EffectIds_Unique);
        yield return Run(nameof(SingleAttribute_CorrectTypeAndCount), SingleAttribute_CorrectTypeAndCount);
        yield return Run(nameof(DualAttribute_CorrectTypeAndCount), DualAttribute_CorrectTypeAndCount);
        yield return Run(nameof(TripleAttribute_CorrectTypeAndCount), TripleAttribute_CorrectTypeAndCount);
        yield return Run(nameof(FourAttribute_CorrectTypeAndCount), FourAttribute_CorrectTypeAndCount);
        yield return Run(nameof(FiveAttribute_CorrectTypeAndCount), FiveAttribute_CorrectTypeAndCount);
        yield return Run(nameof(SixAttribute_CorrectTypeAndCount), SixAttribute_CorrectTypeAndCount);
        yield return Run(nameof(FiveAttribute_Properties), FiveAttribute_Properties);
        yield return Run(nameof(SixAttribute_Properties), SixAttribute_Properties);
        yield return Run(nameof(PassiveProperties_OneToFourAttribute), PassiveProperties_OneToFourAttribute);
        yield return Run(nameof(EffectIdFormat_LowercaseUnderscore), EffectIdFormat_LowercaseUnderscore);
        yield return Run(nameof(FlagCount_MatchesAttributeCount), FlagCount_MatchesAttributeCount);
        yield return Run(nameof(Registry_BidirectionalLookup), Registry_BidirectionalLookup);
        yield return Run(nameof(CareerNames_MatchDocument), CareerNames_MatchDocument);
    }

    private static (string, bool, string) Run(string name, Func<(bool, string)> test)
    {
        try
        {
            var (ok, msg) = test();
            return (name, ok, msg);
        }
        catch (Exception ex)
        {
            return (name, false, $"Exception: {ex.Message}");
        }
    }

    // ========================================
    // Helpers
    // ========================================

    private static (bool, string) Expect(bool condition, string failMsg)
        => condition ? (true, "") : (false, failMsg);

    /// <summary>强制触发注册表加载并返回所有技能</summary>
    private static List<CareerSkillData> GetAllSkills()
    {
        // 通过访问 Registry 触发 EnsureLoaded
        var reg = CareerSkillRegistry.Registry;
        return reg.Values.ToList();
    }

    /// <summary>计算 flags 中的属性位数</summary>
    private static int PopCount(int flags)
    {
        int count = 0;
        for (int i = 0; i < 6; i++)
            if ((flags & (1 << i)) != 0) count++;
        return count;
    }

    // ========================================
    // Tests
    // ========================================

    /// <summary>总共 63 个技能: 6+15+20+15+6+1 = 63</summary>
    private static (bool, string) AllSkills_Count_63()
    {
        var all = GetAllSkills();
        return Expect(all.Count == 63,
            $"expected 63 registered skills, got {all.Count}");
    }

    /// <summary>所有 effectId 唯一 — 无重复</summary>
    private static (bool, string) AllSkills_EffectIds_Unique()
    {
        var all = GetAllSkills();
        var ids = all.Select(s => s.EffectId).ToList();
        var duplicates = ids.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            return (false, $"duplicate effectIds: {string.Join(", ", duplicates)}");
        return (true, "");
    }

    /// <summary>单属性: 6 个, 全是 Passive, AttributeCount=1</summary>
    private static (bool, string) SingleAttribute_CorrectTypeAndCount()
    {
        var all = GetAllSkills().Where(s => s.AttributeCount == 1).ToList();
        if (all.Count != 6)
            return (false, $"expected 6 single-attribute skills, got {all.Count}");
        foreach (var s in all)
        {
            if (!s.IsPassive)
                return (false, $"single-attribute skill '{s.EffectId}' should be Passive, got Active");
        }
        return (true, "");
    }

    /// <summary>双属性: 15 个, 全是 Passive, AttributeCount=2</summary>
    private static (bool, string) DualAttribute_CorrectTypeAndCount()
    {
        var all = GetAllSkills().Where(s => s.AttributeCount == 2).ToList();
        if (all.Count != 15)
            return (false, $"expected 15 dual-attribute skills, got {all.Count}");
        foreach (var s in all)
        {
            if (!s.IsPassive)
                return (false, $"dual-attribute skill '{s.EffectId}' should be Passive, got Active");
        }
        return (true, "");
    }

    /// <summary>三属性: 20 个, 全是 Passive, AttributeCount=3</summary>
    private static (bool, string) TripleAttribute_CorrectTypeAndCount()
    {
        var all = GetAllSkills().Where(s => s.AttributeCount == 3).ToList();
        if (all.Count != 20)
            return (false, $"expected 20 triple-attribute skills, got {all.Count}");
        foreach (var s in all)
        {
            if (!s.IsPassive)
                return (false, $"triple-attribute skill '{s.EffectId}' should be Passive, got Active");
        }
        return (true, "");
    }

    /// <summary>四属性: 15 个, 全是 Passive, AttributeCount=4</summary>
    private static (bool, string) FourAttribute_CorrectTypeAndCount()
    {
        var all = GetAllSkills().Where(s => s.AttributeCount == 4).ToList();
        if (all.Count != 15)
            return (false, $"expected 15 four-attribute skills, got {all.Count}");
        foreach (var s in all)
        {
            if (!s.IsPassive)
                return (false, $"four-attribute skill '{s.EffectId}' should be Passive, got Active");
        }
        return (true, "");
    }

    /// <summary>五属性: 6 个, 全是 Active, AttributeCount=5</summary>
    private static (bool, string) FiveAttribute_CorrectTypeAndCount()
    {
        var all = GetAllSkills().Where(s => s.AttributeCount == 5).ToList();
        if (all.Count != 6)
            return (false, $"expected 6 five-attribute skills, got {all.Count}");
        foreach (var s in all)
        {
            if (!s.IsActive)
                return (false, $"five-attribute skill '{s.EffectId}' should be Active, got Passive");
        }
        return (true, "");
    }

    /// <summary>六属性: 1 个 (万象), Active, AttributeCount=6</summary>
    private static (bool, string) SixAttribute_CorrectTypeAndCount()
    {
        var all = GetAllSkills().Where(s => s.AttributeCount == 6).ToList();
        if (all.Count != 1)
            return (false, $"expected 1 six-attribute skill (Paragon), got {all.Count}");
        var paragon = all[0];
        if (paragon.EffectId != "paragon_all_aspects")
            return (false, $"six-attribute skill should be 'paragon_all_aspects', got '{paragon.EffectId}'");
        if (!paragon.IsActive)
            return (false, "Paragon should be Active");
        return (true, "");
    }

    /// <summary>五属性主动: RequiresFullAp=true, ConsumesMaxAp=true, LimitType=OncePerBattle, ShowInCombatUi=true</summary>
    private static (bool, string) FiveAttribute_Properties()
    {
        var fiveAttr = GetAllSkills().Where(s => s.AttributeCount == 5).ToList();
        foreach (var s in fiveAttr)
        {
            if (!s.RequiresFullAp)
                return (false, $"five-attribute '{s.EffectId}': RequiresFullAp should be true");
            if (!s.ConsumesMaxAp)
                return (false, $"five-attribute '{s.EffectId}': ConsumesMaxAp should be true");
            if (s.LimitType != CareerSkillData.UsageLimit.OncePerBattle)
                return (false, $"five-attribute '{s.EffectId}': should be OncePerBattle, got {s.LimitType}");
            if (!s.ShowInCombatUi)
                return (false, $"five-attribute '{s.EffectId}': ShowInCombatUi should be true");
        }
        return (true, "");
    }

    /// <summary>六属性(万象): OncePerTurn, 不消耗 AP, 无满AP要求, ShowInCombatUi=true</summary>
    private static (bool, string) SixAttribute_Properties()
    {
        var sixAttr = GetAllSkills().Where(s => s.AttributeCount == 6).ToList();
        foreach (var s in sixAttr)
        {
            if (s.RequiresFullAp)
                return (false, $"six-attribute '{s.EffectId}': RequiresFullAp should be false");
            if (s.ConsumesMaxAp)
                return (false, $"six-attribute '{s.EffectId}': ConsumesMaxAp should be false");
            if (s.LimitType != CareerSkillData.UsageLimit.OncePerTurn)
                return (false, $"six-attribute '{s.EffectId}': should be OncePerTurn, got {s.LimitType}");
            if (!s.ShowInCombatUi)
                return (false, $"six-attribute '{s.EffectId}': ShowInCombatUi should be true");
            if (s.ApCost != 0)
                return (false, $"six-attribute '{s.EffectId}': ApCost should be 0");
        }
        return (true, "");
    }

    /// <summary>1-4 属性被动: Type=Passive, ShowInCombatUi=false, RequiresFullAp=false, ConsumesMaxAp=false</summary>
    private static (bool, string) PassiveProperties_OneToFourAttribute()
    {
        var passives = GetAllSkills().Where(s => s.AttributeCount >= 1 && s.AttributeCount <= 4).ToList();
        foreach (var s in passives)
        {
            if (!s.IsPassive)
                return (false, $"passive '{s.EffectId}' (attr={s.AttributeCount}) should have Type=Passive");
            if (s.ShowInCombatUi)
                return (false, $"passive '{s.EffectId}' (attr={s.AttributeCount}): ShowInCombatUi should be false");
            if (s.RequiresFullAp)
                return (false, $"passive '{s.EffectId}' (attr={s.AttributeCount}): RequiresFullAp should be false");
            if (s.ConsumesMaxAp)
                return (false, $"passive '{s.EffectId}' (attr={s.AttributeCount}): ConsumesMaxAp should be false");
        }
        return (true, "");
    }

    /// <summary>effectId 格式: 小写字母 + 数字 + 下划线（如 champion_move_3_boost_ally）</summary>
    private static (bool, string) EffectIdFormat_LowercaseUnderscore()
    {
        var all = GetAllSkills();
        foreach (var s in all)
        {
            foreach (char c in s.EffectId)
            {
                if (!char.IsLower(c) && !char.IsDigit(c) && c != '_')
                    return (false, $"effectId '{s.EffectId}' contains invalid char '{c}' — only lowercase, digits and underscores allowed");
            }
        }
        return (true, "");
    }

    /// <summary>RequiredTitleFlags 的 popcount 与 AttributeCount 一致</summary>
    private static (bool, string) FlagCount_MatchesAttributeCount()
    {
        var all = GetAllSkills();
        foreach (var s in all)
        {
            int flagCount = PopCount(s.RequiredTitleFlags);
            if (flagCount != s.AttributeCount)
                return (false, $"skill '{s.EffectId}': RequiredTitleFlags popcount={flagCount} but AttributeCount={s.AttributeCount}");
        }
        return (true, "");
    }

    /// <summary>Registry 双向查找一致: 通过 flags 和 effectId 找到同一技能</summary>
    private static (bool, string) Registry_BidirectionalLookup()
    {
        var byFlags = CareerSkillRegistry.Registry;
        var byEffectId = CareerSkillRegistry.ByEffectId;

        // 所有通过 flags 注册的技能也应能通过 effectId 找到
        foreach (var kv in byFlags)
        {
            var fromFlags = kv.Value;
            var fromEffectId = CareerSkillRegistry.GetByEffectId(fromFlags.EffectId);
            if (fromEffectId == null)
                return (false, $"effectId '{fromFlags.EffectId}' not found via GetByEffectId");
            if (fromEffectId.EffectId != fromFlags.EffectId)
                return (false, $"lookup mismatch: flags={kv.Key} -> '{fromFlags.EffectId}', effectId lookup -> '{fromEffectId.EffectId}'");
        }

        // ByEffectId count matches Registry count
        if (byEffectId.Count != byFlags.Count)
            return (false, $"Registry count ({byFlags.Count}) != ByEffectId count ({byEffectId.Count})");

        return (true, "");
    }

    /// <summary>职业名与 docs/职业专属技能.md 保持一致。</summary>
    private static (bool, string) CareerNames_MatchDocument()
    {
        var expected = new Dictionary<int, (string Chinese, string English)>
        {
            { 1, ("战士", "Warrior") }, { 2, ("游侠", "Ranger") }, { 4, ("守卫", "Guardian") },
            { 8, ("法师", "Mage") }, { 16, ("刺客", "Assassin") }, { 32, ("诗人", "Bard") },
            { 3, ("剑舞者", "Blade Dancer") }, { 5, ("重战士", "Juggernaut") },
            { 9, ("魔剑士", "Spellsword") }, { 17, ("处刑人", "Executioner") },
            { 33, ("征讨者", "Warlord") }, { 6, ("决斗者", "Duelist") },
            { 10, ("秘射手", "Arcane Archer") }, { 18, ("狩猎者", "Falconer") },
            { 34, ("游荡者", "Rogue") }, { 12, ("战法师", "Battlemage") },
            { 20, ("苦修者", "Veteran") }, { 36, ("守御者", "Iron Commander") },
            { 24, ("大贤者", "Sage") }, { 40, ("指引者", "Sorcerer") },
            { 48, ("预言者", "Prophet") }, { 7, ("大宗师", "Grandmaster") },
            { 11, ("魔武者", "Spellweaver") }, { 19, ("审判官", "Hawkeye") },
            { 35, ("战誓者", "Champion") }, { 13, ("述法者", "Ironweaver") },
            { 21, ("惩罚者", "Skullcrusher") }, { 37, ("征服者", "Conqueror") },
            { 25, ("毁灭者", "Doom Knight") }, { 41, ("支配者", "Overlord") },
            { 49, ("十字军", "Crusader") }, { 14, ("影缄者", "Shadow Shroud") },
            { 22, ("鹰眼卫", "Hawkeye Guard") }, { 38, ("游骑兵", "Outrider") },
            { 26, ("唤星者", "Starcaller") }, { 42, ("幻术师", "Illusionist") },
            { 50, ("风语者", "Windwalker") }, { 28, ("敌法师", "Antimage") },
            { 44, ("铁幕领主", "Iron Sovereign") }, { 52, ("誓盾卫", "Oathshield") },
            { 56, ("天选者", "Chosen One") }, { 60, ("秘院贤师", "Archsage") },
            { 58, ("灵风秘庭", "Zephyr Master") }, { 54, ("荒原之心", "Warchief") },
            { 46, ("血契之环", "Blood Pact") }, { 30, ("静默之刃", "Silent Edge") },
            { 57, ("毁灭王冠", "Crown of Ruin") }, { 53, ("磐石守护", "Stone Saint") },
            { 45, ("铁铸领主", "Ironbound Lord") }, { 29, ("渊狱骑士", "Void Knight") },
            { 51, ("战争之风", "Storm Banner") }, { 43, ("焰风之怒", "Tempest Wrath") },
            { 27, ("孤刃之誓", "Lone Blade") }, { 39, ("战争领主", "War King") },
            { 23, ("钢弦骑士", "Steelstring Knight") }, { 15, ("鏖战骑士", "Arcane War Knight") },
            { 62, ("万灵之约印", "Emissary") }, { 61, ("山岳之王座", "Mountain Lord") },
            { 59, ("星界之裂隙", "Astral Walker") }, { 55, ("荒芜之化身", "Wrath Avatar") },
            { 47, ("铁血之律令", "Iron Tyrant") }, { 31, ("孤星之刃影", "Lone Shadow") },
            { 63, ("万象", "Paragon") },
        };

        foreach (var (flags, names) in expected)
        {
            var skill = CareerSkillRegistry.GetByTitleFlags(flags);
            if (skill == null)
                return (false, $"flags {flags}: missing career skill");
            if (skill.DisplayName != names.Chinese)
                return (false, $"flags {flags}: expected display '{names.Chinese}', got '{skill.DisplayName}'");
            if (skill.EnglishName != names.English)
                return (false, $"flags {flags}: expected english '{names.English}', got '{skill.EnglishName}'");

            string title = ClassTitleResolver.GetTitleByFlags(flags);
            if (title != names.Chinese)
                return (false, $"flags {flags}: expected title '{names.Chinese}', got '{title}'");
        }

        return (true, "");
    }
}
