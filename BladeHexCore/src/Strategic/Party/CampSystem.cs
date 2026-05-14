// CampSystem.cs
// 营地休息系统 — 大地图上扎营恢复 HP，消耗食物和时间
using Godot;
using System;
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// 营地休息结果
/// </summary>
public class CampResult
{
    public bool Success;
    public string Message = "";
    public int HpRestored;
    public float FoodConsumed;
    public int HoursElapsed;
    public bool LeveledUp; // 是否有人升级
}

/// <summary>
/// 营地系统
/// </summary>
public static class CampSystem
{
    /// <summary>休息消耗的食物（每人）</summary>
    public const float FoodPerPersonPerRest = 1.0f;

    /// <summary>休息恢复的 HP（基础）</summary>
    public const int BaseHpRestore = 5;

    /// <summary>休息耗时（小时）</summary>
    public const int RestHours = 8;

    /// <summary>
    /// 执行扎营休息
    /// </summary>
    public static CampResult Rest(PartyRoster roster, ref float currentFood, int partySize)
    {
        var result = new CampResult { HoursElapsed = RestHours };

        // 检查食物
        float needed = partySize * FoodPerPersonPerRest;
        if (currentFood < needed * 0.5f)
        {
            result.Success = false;
            result.Message = "食物不足，无法扎营休息";
            return result;
        }

        // 消耗食物
        float consumed = Math.Min(needed, currentFood);
        currentFood -= consumed;
        result.FoodConsumed = consumed;

        // 恢复 HP（食物充足恢复更多）
        int hpRestore = consumed >= needed ? BaseHpRestore + 3 : BaseHpRestore;
        roster.RestoreHp(hpRestore);
        result.HpRestored = hpRestore;

        // 检查升级
        result.LeveledUp = CheckAndApplyLevelUps(roster);

        result.Success = true;
        result.Message = $"休息完毕，全员恢复 {hpRestore} HP";
        if (result.LeveledUp) result.Message += "（有队员升级！）";

        GD.Print($"[Camp] 扎营休息: HP+{hpRestore}, 食物-{consumed:F1}, 耗时{RestHours}h");
        return result;
    }

    /// <summary>检查并应用升级</summary>
    public static bool CheckAndApplyLevelUps(PartyRoster roster)
    {
        bool anyLevelUp = false;
        foreach (var member in roster.Members)
        {
            if (CanLevelUp(member))
            {
                ApplyLevelUp(member);
                anyLevelUp = true;
            }
        }
        return anyLevelUp;
    }

    /// <summary>是否可以升级</summary>
    public static bool CanLevelUp(UnitData unit)
    {
        int required = GetXpForNextLevel(unit.Level);
        return unit.Xp >= required;
    }

    /// <summary>
    /// 下一级所需经验（对应策划案：300 + (level - 2) × 200）
    /// Level 1→2: 300, 2→3: 500, 3→4: 700, ...
    /// </summary>
    public static int GetXpForNextLevel(int currentLevel)
    {
        if (currentLevel < 2) return 300;
        return 300 + (currentLevel - 1) * 200;
    }

    /// <summary>
    /// 应用升级 — 只扣 XP 和提升等级，属性点留给玩家分配
    /// 自动提升：HP 上限（按新 CON 修正重算）
    /// 手动分配：+1 属性点（存入 UnspentAttrPoints）
    /// </summary>
    public static void ApplyLevelUp(UnitData unit)
    {
        int required = GetXpForNextLevel(unit.Level);
        unit.Xp -= required;
        unit.Level += 1;

        // +1 未分配属性点（玩家自由分配到 STR/DEX/CON/INT/WIS/CHA）
        unit.UnspentAttrPoints += 1;

        // HP 上限重算：BaseHP + Mod(CON) × Level
        // Mod(CON) = floor(sqrt(CON / 2))
        int conMod = (int)Math.Floor(Math.Sqrt(unit.Con / 2.0));
        int oldMaxHp = unit.BaseMaxHp;
        // BaseHP 不变，只是因为 level 增加了所以 MaxHP 增加 conMod
        unit.BaseMaxHp += conMod;
        // 当前 HP 也增加等量（不超过新上限）
        int currentHp = PartyRoster.GetCurrentHp(unit);
        PartyRoster.SetCurrentHp(unit, Math.Min(currentHp + conMod, unit.BaseMaxHp));

        // 技能点 +1
        unit.SkillPoints += 1;

        GD.Print($"[LevelUp] {unit.UnitName} 升到 Lv{unit.Level}! MaxHP+{conMod} (→{unit.BaseMaxHp}), 待分配属性点: {unit.UnspentAttrPoints}");
    }

    /// <summary>
    /// 分配属性点（玩家操作）
    /// </summary>
    /// <param name="unit">目标单位</param>
    /// <param name="statIndex">0=STR, 1=DEX, 2=CON, 3=INT, 4=WIS, 5=CHA</param>
    /// <returns>是否成功</returns>
    public static bool AllocateAttributePoint(UnitData unit, int statIndex)
    {
        if (unit.UnspentAttrPoints <= 0) return false;
        if (statIndex < 0 || statIndex > 5) return false;

        switch (statIndex)
        {
            case 0: unit.Str += 1; break;
            case 1: unit.Dex += 1; break;
            case 2: unit.Con += 1; break;
            case 3: unit.Intel += 1; break;
            case 4: unit.Wis += 1; break;
            case 5: unit.Cha += 1; break;
        }

        unit.UnspentAttrPoints -= 1;

        // 如果加了 CON，重算 MaxHP
        if (statIndex == 2)
        {
            int newConMod = (int)Math.Floor(Math.Sqrt(unit.Con / 2.0));
            int oldConMod = (int)Math.Floor(Math.Sqrt((unit.Con - 1) / 2.0));
            if (newConMod > oldConMod)
            {
                // CON 修正值提升了 → MaxHP 增加 Level 点
                unit.BaseMaxHp += unit.Level;
                int currentHp = PartyRoster.GetCurrentHp(unit);
                PartyRoster.SetCurrentHp(unit, currentHp + unit.Level);
            }
        }

        return true;
    }
}
