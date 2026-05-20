using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Combat;

/// <summary>
/// 技能注册表 — 从 JSON 加载技能效果配置
/// 作为技能数据的单一真相源
/// </summary>
public static class SkillRegistry
{
    // ============================================================================
    // 技能分类与目标类型
    // ============================================================================

    public enum SkillCategory
    {
        MeleeActive,    // 近战主动
        RangedActive,   // 远程主动
        MagicActive,    // 法术主动
        HealActive,     // 治疗主动
        SupportActive,  // 辅助主动
        Passive,        // 被动（常驻修正）
        Keystone,       // 代价型被动
        OutOfCombat,    // 非战斗效果
    }

    public enum TargetType
    {
        Self,           // 自身
        SingleEnemy,    // 单个敌人
        SingleAlly,     // 单个友军（含自身）
        AllAdjacent,    // 周围所有（六邻格）
        AoeSmall,       // 小范围 AoE（半径1）
        AoeCone,        // 锥形范围
        RangedSingle,   // 远程单体
        RangedAoe,      // 远程 AoE
        AllAllies,      // 所有友军
    }

    // ============================================================================
    // JSON 驱动加载
    // ============================================================================

    private static Dictionary<string, Godot.Collections.Dictionary>? _cached;
    private const string JsonPath = "res://BladeHexFrontend/src/View/Combat/skill_configs.json";
    private const string ModPath = "user://mods/skills_config/";

    private static Dictionary<string, Godot.Collections.Dictionary> Registry
    {
        get
        {
            if (_cached != null) return _cached;
            _cached = LoadFromJson();
            return _cached;
        }
    }

    /// <summary>强制重新加载（热重载用）</summary>
    public static void Reload() { _cached = null; }

    // ============================================================================
    // 查询接口
    // ============================================================================

    public static Godot.Collections.Dictionary GetSkillConfig(string skillEffect)
    {
        if (Registry.TryGetValue(skillEffect, out var config)) return config;
        return new Godot.Collections.Dictionary();
    }

    public static bool IsActiveSkill(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.Count == 0) return false;
        int cat = cfg["category"].AsInt32();
        return cat == (int)SkillCategory.MeleeActive || cat == (int)SkillCategory.RangedActive ||
               cat == (int)SkillCategory.MagicActive || cat == (int)SkillCategory.HealActive ||
               cat == (int)SkillCategory.SupportActive;
    }

    public static bool IsPassiveSkill(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.Count == 0) return false;
        int cat = cfg["category"].AsInt32();
        return cat == (int)SkillCategory.Passive || cat == (int)SkillCategory.Keystone || cat == (int)SkillCategory.OutOfCombat;
    }

    public static string[] GetAllActiveSkillIds()
    {
        return Registry.Keys.Where(IsActiveSkill).ToArray();
    }

    /// <summary>
    /// 是否为 Spell（v0.6 10.0）。Spell 不计入"每回合 1 次非 Spell 主动技能"限制，
    /// 但需要法术媒介、不能持盾、只能穿布甲，并消耗 Mana。
    /// </summary>
    public static bool IsSpell(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.ContainsKey("is_spell")) return cfg["is_spell"].AsBool();
        // 兜底：MagicActive 视为 Spell
        if (cfg.ContainsKey("category"))
        {
            int cat = cfg["category"].AsInt32();
            if (cat == (int)SkillCategory.MagicActive) return true;
        }
        return false;
    }

    /// <summary>Spell 的 Mana 消耗。非 Spell 或未配置返回 0。</summary>
    public static int GetManaCost(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.ContainsKey("mana_cost")) return cfg["mana_cost"].AsInt32();
        return 0;
    }

    /// <summary>技能施法/攻击射程（格）。0=自身，1=近战邻格。</summary>
    public static int GetRange(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.ContainsKey("range")) return cfg["range"].AsInt32();
        // 根据 target 类型推断默认值
        string target = cfg.ContainsKey("target") ? cfg["target"].AsString() : "Self";
        return target switch
        {
            "Self" => 0,
            "SingleEnemy" or "AllAdjacent" => 1,
            "RangedSingle" or "RangedAoe" => 6,
            "AoeSmall" or "AoeCone" => 4,
            "SingleAlly" => 3,
            "AllAllies" => 0,
            _ => 1,
        };
    }

    /// <summary>技能 AOE 半径（格）。0=单体/无溅射。</summary>
    public static int GetAoeRadius(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.ContainsKey("aoe_radius")) return cfg["aoe_radius"].AsInt32();
        // 根据 target 类型推断
        string target = cfg.ContainsKey("target") ? cfg["target"].AsString() : "Self";
        return target switch
        {
            "AllAdjacent" => 1,
            "AoeSmall" => 1,
            "RangedAoe" => 2,
            _ => 0,
        };
    }

    /// <summary>获取技能目标类型字符串。</summary>
    public static string GetTargetType(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        return cfg.ContainsKey("target") ? cfg["target"].AsString() : "Self";
    }

    // ============================================================================
    // JSON 解析
    // ============================================================================

    private static Dictionary<string, Godot.Collections.Dictionary> LoadFromJson()
    {
        var dict = new Dictionary<string, Godot.Collections.Dictionary>();

        // 内置技能
        LoadSkillsFromFile(JsonPath, dict);

        // Mod 技能
        if (DirAccess.DirExistsAbsolute(ModPath))
        {
            using var dir = DirAccess.Open(ModPath);
            if (dir != null)
            {
                dir.ListDirBegin();
                string fileName = dir.GetNext();
                while (!string.IsNullOrEmpty(fileName))
                {
                    if (fileName.EndsWith(".json"))
                        LoadSkillsFromFile(ModPath + fileName, dict);
                    fileName = dir.GetNext();
                }
                dir.ListDirEnd();
            }
        }

        if (dict.Count == 0)
        {
            GD.PushError("[SkillRegistry] No skills loaded! Check skill_configs.json.");
        }
        else
        {
            GD.Print($"[SkillRegistry] Loaded {dict.Count} skills");
        }

        return dict;
    }

    private static void LoadSkillsFromFile(string path, Dictionary<string, Godot.Collections.Dictionary> dict)
    {
        if (!FileAccess.FileExists(path)) return;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return;

        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
        {
            GD.PushError($"[SkillRegistry] JSON parse error in {path}: {json.GetErrorMessage()}");
            return;
        }

        if (json.Data.VariantType != Variant.Type.Array) return;
        var arr = json.Data.AsGodotArray();

        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i].VariantType != Variant.Type.Dictionary) continue;
            var entry = arr[i].AsGodotDictionary();

            try
            {
                var skillDict = ParseSkillEntry(entry);
                string id = entry.ContainsKey("id") ? entry["id"].AsString() : "";
                if (!string.IsNullOrEmpty(id))
                    dict[id] = skillDict;
            }
            catch (Exception ex)
            {
                GD.PushError($"[SkillRegistry] Failed to parse {path}[{i}]: {ex.Message}");
            }
        }
    }

    private static Godot.Collections.Dictionary ParseSkillEntry(Godot.Collections.Dictionary entry)
    {
        var result = new Godot.Collections.Dictionary();

        // category: string -> int
        if (entry.ContainsKey("category"))
        {
            string catStr = entry["category"].AsString();
            if (Enum.TryParse<SkillCategory>(catStr, out var cat))
                result["category"] = (int)cat;
            else
                result["category"] = 0;
        }

        // target: string -> int
        if (entry.ContainsKey("target"))
        {
            string tgtStr = entry["target"].AsString();
            if (Enum.TryParse<TargetType>(tgtStr, out var tgt))
                result["target"] = (int)tgt;
            else
                result["target"] = 0;
        }

        // 直接复制的字段
        if (entry.ContainsKey("name")) result["name"] = entry["name"];
        if (entry.ContainsKey("description")) result["description"] = entry["description"];
        if (entry.ContainsKey("vfx")) result["vfx"] = entry["vfx"];
        if (entry.ContainsKey("action_cost")) result["action_cost"] = entry["action_cost"].AsInt32();
        if (entry.ContainsKey("mana_cost")) result["mana_cost"] = entry["mana_cost"].AsInt32();
        if (entry.ContainsKey("is_spell")) result["is_spell"] = entry["is_spell"].AsBool();
        if (entry.ContainsKey("cost")) result["cost"] = entry["cost"];
        if (entry.ContainsKey("range")) result["range"] = entry["range"].AsInt32();

        return result;
    }
}
