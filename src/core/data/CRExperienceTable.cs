// CRExperienceTable.cs
// 威胁等级(CR)经验表 + 遭遇预算系统
// 对应策划案 08-敌方与AI.md §4
// 适配120级体系：CR = floor(level / 6)
using Godot;

namespace BladeHex.Data;

[GlobalClass]
public partial class CRExperienceTable : RefCounted
{
    // ========================================
    // 枚举
    // ========================================

    public enum CRLevel { Grunt, Standard, Elite, Boss, Legendary, Mythic }

    public enum EncounterDifficulty { Easy, Standard, Hard, Deadly, BossFight }

    // ========================================
    // CR → 经验值对照表
    // ========================================

    private static readonly Godot.Collections.Dictionary CrToXp = new()
    {
        { 0.0, 10 }, { 0.25, 25 }, { 0.5, 50 },
        { 1.0, 200 }, { 2.0, 450 }, { 3.0, 700 },
        { 4.0, 1100 }, { 5.0, 1800 }, { 6.0, 2300 },
        { 7.0, 2900 }, { 8.0, 3900 }, { 9.0, 5000 },
        { 10.0, 5900 }, { 11.0, 7200 }, { 12.0, 8400 },
        { 13.0, 10000 }, { 14.0, 11500 }, { 15.0, 13000 },
        { 16.0, 15000 }, { 17.0, 18000 }, { 18.0, 20000 },
        { 19.0, 22000 }, { 20.0, 25000 },
    };

    // ========================================
    // CR → XP 查询
    // ========================================

    public static int GetXpForCr(float cr)
    {
        if (CrToXp.ContainsKey(cr))
            return CrToXp[cr].AsInt32();
        if (cr >= 21.0)
            return (int)(33000 + (cr - 21.0) * 5000);
        return 10;
    }

    public static int GetEncounterTotalXp(float[] enemyCrList)
    {
        int total = 0;
        foreach (var cr in enemyCrList)
            total += GetXpForCr(cr);
        return total;
    }

    public static float GetCrFromLevel(int level) => RPGRuleEngine.GetCrFromLevel(level);
    public static int GetLevelFromCr(float cr) => RPGRuleEngine.GetLevelFromCr(cr);

    // ========================================
    // CR 等级定位
    // ========================================

    public static int GetCrLevel(float cr)
    {
        if (cr < 1.0) return (int)CRLevel.Grunt;
        if (cr <= 3.0) return (int)CRLevel.Standard;
        if (cr <= 7.0) return (int)CRLevel.Elite;
        if (cr <= 12.0) return (int)CRLevel.Boss;
        if (cr <= 20.0) return (int)CRLevel.Legendary;
        return (int)CRLevel.Mythic;
    }

    public static string GetCrLevelName(float cr) => GetCrLevel(cr) switch
    {
        (int)CRLevel.Grunt => "杂兵",
        (int)CRLevel.Standard => "熟练",
        (int)CRLevel.Elite => "精英",
        (int)CRLevel.Boss => "首领",
        (int)CRLevel.Legendary => "传奇",
        (int)CRLevel.Mythic => "神话",
        _ => "未知",
    };

    // ========================================
    // 遭遇CR预算
    // ========================================

    public static float GetEncounterBudget(int partyAvgLevel, int difficulty)
    {
        float partyCr = RPGRuleEngine.GetCrFromLevel(partyAvgLevel);
        return difficulty switch
        {
            (int)EncounterDifficulty.Easy => partyCr * 0.5f,
            (int)EncounterDifficulty.Standard => partyCr * 1.0f,
            (int)EncounterDifficulty.Hard => partyCr * 1.5f,
            (int)EncounterDifficulty.Deadly => partyCr * 2.0f,
            (int)EncounterDifficulty.BossFight => RPGRuleEngine.GetCrFromLevel(partyAvgLevel + 24),
            _ => partyCr * 1.0f,
        };
    }

    public static int AssessEncounter(int partyAvgLevel, float[] enemyCrList)
    {
        int budget = GetEncounterTotalXp(enemyCrList);
        float partyCr = RPGRuleEngine.GetCrFromLevel(partyAvgLevel);
        int partyXpThreshold = GetXpForCr(partyCr);

        float adjustedThreshold = partyXpThreshold;
        if (enemyCrList.Length >= 5)
            adjustedThreshold *= 2.0f;
        else if (enemyCrList.Length >= 3)
            adjustedThreshold *= 1.5f;

        if (budget <= partyXpThreshold * 0.5)
            return (int)EncounterDifficulty.Easy;
        if (budget <= partyXpThreshold)
            return (int)EncounterDifficulty.Standard;
        if (budget <= partyXpThreshold * 1.5)
            return (int)EncounterDifficulty.Hard;
        return (int)EncounterDifficulty.Deadly;
    }

    public static string GetDifficultyName(int difficulty) => difficulty switch
    {
        (int)EncounterDifficulty.Easy => "轻松",
        (int)EncounterDifficulty.Standard => "标准",
        (int)EncounterDifficulty.Hard => "困难",
        (int)EncounterDifficulty.Deadly => "致命",
        (int)EncounterDifficulty.BossFight => "Boss",
        _ => "未知",
    };

    // ========================================
    // CR 对等规则
    // ========================================

    public static Godot.Collections.Dictionary CanEngage(int partyAvgLevel, float highestEnemyCr)
    {
        float partyCr = RPGRuleEngine.GetCrFromLevel(partyAvgLevel);
        float diff = highestEnemyCr - partyCr;
        if (diff <= 2.0)
            return new() { { "can_fight", true }, { "warning", "" } };
        if (diff <= 5.0)
            return new() { { "can_fight", true }, { "warning", "极难遭遇，建议做好准备！" } };
        return new() { { "can_fight", false }, { "warning", "敌人远超队伍实力，强烈建议撤退！" } };
    }
}
