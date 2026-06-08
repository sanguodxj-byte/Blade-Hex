using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BladeHex.Data;
using BladeHex.Map;

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
        Ground,         // 空地/地面格
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
        if (IsIntrinsicSkillName(skillEffect)) return CreateIntrinsicSkillConfig(skillEffect);
        if (IsFixedSkillTreePassiveEffect(skillEffect)) return CreateFixedSkillTreePassiveConfig(skillEffect);
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

    public static bool IsMeleeActive(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.Count == 0 || !cfg.ContainsKey("category")) return false;
        return cfg["category"].AsInt32() == (int)SkillCategory.MeleeActive;
    }

    public static bool IsEquippableCombatSkill(string skillEffect)
    {
        if (string.IsNullOrEmpty(skillEffect)) return false;
        if (skillEffect.StartsWith("spell_slot_", StringComparison.Ordinal)) return false;
        return IsActiveSkill(skillEffect);
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

    /// <summary>技能的 AP (行动力) 消耗。默认返回 4。</summary>
    public static int GetActionCost(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.ContainsKey("action_cost")) return cfg["action_cost"].AsInt32();
        return 4;
    }

    public static int GetActionCost(string skillEffect, Unit? caster)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.ContainsKey("movement_ap_bonus"))
            return GetActionCost(skillEffect);
        if (cfg.ContainsKey("weapon_ap_bonus"))
        {
            int weaponAp = caster?.Model.GetMainHand() is BladeHex.Data.WeaponData weapon ? weapon.ApCost : 4;
            return Math.Max(0, weaponAp + cfg["weapon_ap_bonus"].AsInt32());
        }

        return GetActionCost(skillEffect);
    }

    public static int GetActionCost(string skillEffect, Unit? caster, Vector2I targetCell)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.ContainsKey("movement_ap_bonus") && caster != null && GodotObject.IsInstanceValid(caster))
        {
            int distance = HexUtils.AxialDistance(caster.GridPos, targetCell);
            return Math.Max(0, distance + cfg["movement_ap_bonus"].AsInt32());
        }

        return GetActionCost(skillEffect, caster);
    }

    public static int GetUsesPerBattle(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.ContainsKey("uses_per_battle")) return cfg["uses_per_battle"].AsInt32();
        return -1;
    }


    /// <summary>技能施法/攻击射程（格）。0=自身，1=近战邻格。</summary>
    public static int GetRange(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (cfg.ContainsKey("range")) return cfg["range"].AsInt32();
        // 根据 target 类型推断默认值（target_str 由 ParseSkillEntry 同步存入）
        string target = cfg.ContainsKey("target_str") ? cfg["target_str"].AsString() : "Self";
        return target switch
        {
            "Self" => 0,
            "SingleEnemy" or "AllAdjacent" => 1,
            "RangedSingle" or "RangedAoe" => 6,
            "Ground" => 4,
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
        string target = cfg.ContainsKey("target_str") ? cfg["target_str"].AsString() : "Self";
        return target switch
        {
            "AllAdjacent" => 1,
            "AoeSmall" => 1,
            "RangedAoe" => 2,
            _ => 0,
        };
    }

    /// <summary>
    /// 获取技能缩放配置。返回 (Sides, Stat, StatMult)，无配置返回 null。
    /// 数据来源：skill_configs.json 的 "scaling" 字段。
    /// </summary>
    public static (int Sides, string Stat, float StatMult)? GetScaling(string skillId)
    {
        var cfg = GetSkillConfig(skillId);
        if (!cfg.ContainsKey("scaling")) return null;
        var s = cfg["scaling"].AsGodotDictionary();
        int sides = s.ContainsKey("sides") ? s["sides"].AsInt32() : 6;
        string stat = s.ContainsKey("stat") ? s["stat"].AsString() : "str";
        float mult = s.ContainsKey("stat_mult") ? (float)s["stat_mult"].AsDouble() : 1.0f;
        return (sides, stat, mult);
    }

    /// <summary>获取技能目标类型字符串。</summary>
    public static string GetTargetType(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        return cfg.ContainsKey("target_str") ? cfg["target_str"].AsString() : "Self";
    }

    public static string GetEquipmentRequirementText(string skillEffect)
    {
        var cfg = GetSkillConfig(skillEffect);
        if (!cfg.ContainsKey("requires_equipment")) return "";
        return cfg["requires_equipment"].AsString() switch
        {
            "melee" => "近战武器",
            "ranged" => "远程武器",
            "melee_shield" => "近战武器 + 盾牌",
            "light_weapon" => "轻型近战武器",
            _ => "",
        };
    }

    public static bool CanUseWithEquipment(string skillEffect, Unit? caster, out string reason)
    {
        reason = "";
        var cfg = GetSkillConfig(skillEffect);
        if (!cfg.ContainsKey("requires_equipment")) return true;
        if (caster == null || !GodotObject.IsInstanceValid(caster))
        {
            reason = "施放者无效";
            return false;
        }

        var weapon = caster.Model.GetMainHand() as WeaponData;
        bool hasShield = caster.Model.GetOffHand() is ArmorData { armorType: ArmorData.ArmorType.Shield }
            || caster.Data?.Shield is ArmorData { armorType: ArmorData.ArmorType.Shield };

        switch (cfg["requires_equipment"].AsString())
        {
            case "melee":
                if (weapon != null && !weapon.IsRanged && !weapon.IsCatalyst) return true;
                reason = "需要近战武器";
                return false;
            case "ranged":
                if (weapon != null && weapon.IsRanged && !weapon.IsCatalyst) return true;
                reason = "需要远程武器";
                return false;
            case "melee_shield":
                if (weapon == null || weapon.IsRanged || weapon.IsCatalyst)
                {
                    reason = "需要近战武器";
                    return false;
                }
                if (!hasShield)
                {
                    reason = "需要装备盾牌";
                    return false;
                }
                return true;
            case "light_weapon":
                if (weapon != null && !weapon.IsRanged && !weapon.IsCatalyst && weapon.Weight == WeaponData.WeightCategory.Light)
                    return true;
                reason = "需要轻型近战武器";
                return false;
            default:
                return true;
        }
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

        // target: 同时存储字符串原值（"SingleEnemy" 等，给颜色 switch / range fallback 用）
        // 和 int 版（给 tooltip 数值比较用）。两套读法都得正确。
        if (entry.ContainsKey("target"))
        {
            string tgtStr = entry["target"].AsString();
            result["target_str"] = tgtStr;
            if (Enum.TryParse<TargetType>(tgtStr, out var tgt))
                result["target"] = (int)tgt;
            else
                result["target"] = 0;
        }

        // 直接复制的字段
        if (entry.ContainsKey("name")) result["name"] = entry["name"];
        if (entry.ContainsKey("description")) result["description"] = entry["description"];
        if (entry.ContainsKey("vfx")) result["vfx"] = entry["vfx"];
        if (entry.ContainsKey("sfx")) result["sfx"] = entry["sfx"];
        if (entry.ContainsKey("action_cost")) result["action_cost"] = entry["action_cost"].AsInt32();
        if (entry.ContainsKey("mana_cost")) result["mana_cost"] = entry["mana_cost"].AsInt32();
        if (entry.ContainsKey("is_spell")) result["is_spell"] = entry["is_spell"].AsBool();
        if (entry.ContainsKey("cost")) result["cost"] = entry["cost"];
        if (entry.ContainsKey("range")) result["range"] = entry["range"].AsInt32();
        if (entry.ContainsKey("aoe_radius")) result["aoe_radius"] = entry["aoe_radius"].AsInt32();
        if (entry.ContainsKey("cooldown")) result["cooldown"] = entry["cooldown"].AsInt32();
        if (entry.ContainsKey("uses_per_battle")) result["uses_per_battle"] = entry["uses_per_battle"].AsInt32();
        if (entry.ContainsKey("weapon_ap_bonus")) result["weapon_ap_bonus"] = entry["weapon_ap_bonus"].AsInt32();
        if (entry.ContainsKey("movement_ap_bonus")) result["movement_ap_bonus"] = entry["movement_ap_bonus"].AsInt32();
        if (entry.ContainsKey("requires_equipment")) result["requires_equipment"] = entry["requires_equipment"].AsString();

        // scaling: { sides, stat, stat_mult }
        if (entry.ContainsKey("scaling") && entry["scaling"].VariantType == Variant.Type.Dictionary)
        {
            var src = entry["scaling"].AsGodotDictionary();
            var scalingDict = new Godot.Collections.Dictionary();
            if (src.ContainsKey("sides")) scalingDict["sides"] = src["sides"].AsInt32();
            if (src.ContainsKey("stat")) scalingDict["stat"] = src["stat"].AsString();
            if (src.ContainsKey("stat_mult")) scalingDict["stat_mult"] = (float)src["stat_mult"].AsDouble();
            result["scaling"] = scalingDict;
        }

        return result;
    }

    private static readonly HashSet<string> IntrinsicSkillNames = new()
    {
        "撕咬", "扑击", "嗥叫", "撕裂", "践踏", "毒雾吐息", "火焰冲锋", "恶魔猛击", "恐惧凝视",
        "黑暗劈斩", "亡灵哀嚎", "灵魂汲取", "恐惧之触", "冰霜龙息", "尾击", "翼击", "碾压",
        "恐惧威慑", "烈焰风暴", "岩石投掷", "地震", "巨拳猛击", "月华剑舞", "星辰之力",
        "精灵之歌", "时空裂隙", "酸液喷吐", "吞噬", "钻地突袭", "尾鞭", "麻痹毒刺", "吞噬尸体", "猛击"
    };

    public static bool IsIntrinsicSkillName(string skillEffect)
    {
        return IntrinsicSkillNames.Contains(skillEffect);
    }

    private static bool IsFixedSkillTreePassiveEffect(string skillEffect)
    {
        if (string.IsNullOrEmpty(skillEffect) || !skillEffect.EndsWith("_passive", StringComparison.Ordinal))
            return false;

        var parts = skillEffect.Split('_');
        if (parts.Length != 3) return false;
        if (parts[0] is not ("str" or "dex" or "con" or "int" or "wis" or "cha")) return false;
        return parts[1].Length == 3
            && parts[1][0] == 'p'
            && char.IsDigit(parts[1][1])
            && char.IsDigit(parts[1][2]);
    }

    private static Godot.Collections.Dictionary CreateFixedSkillTreePassiveConfig(string skillEffect)
    {
        var result = new Godot.Collections.Dictionary
        {
            ["category"] = (int)SkillCategory.Passive,
            ["target_str"] = "Self",
            ["target"] = (int)TargetType.Self,
            ["name"] = "技能星盘被动",
            ["description"] = "固定被动节点，具体数值来自技能星盘节点。",
            ["mana_cost"] = 0,
            ["is_spell"] = false,
        };
        return result;
    }

    private static Godot.Collections.Dictionary CreateIntrinsicSkillConfig(string name)
    {
        var result = new Godot.Collections.Dictionary();
        result["name"] = name;

        if (name == "恐惧凝视" || name == "恐惧术" || name == "恐惧威慑" || name == "嗥叫" || name == "精灵之歌" || name == "亡灵哀嚎")
        {
            result["category"] = (int)SkillCategory.SupportActive;
            result["target_str"] = "AllAdjacent";
            result["target"] = (int)TargetType.AllAdjacent;
            result["aoe_radius"] = 2;
        }
        else if (name == "冰霜龙息" || name == "毒雾吐息" || name == "酸液喷吐")
        {
            result["category"] = (int)SkillCategory.MagicActive;
            result["target_str"] = "AoeCone";
            result["target"] = (int)TargetType.AoeCone;
            result["aoe_radius"] = 2;
        }
        else if (name == "岩石投掷" || name == "投石")
        {
            result["category"] = (int)SkillCategory.RangedActive;
            result["target_str"] = "RangedSingle";
            result["target"] = (int)TargetType.RangedSingle;
            result["range"] = 8;
        }
        else
        {
            result["category"] = (int)SkillCategory.MeleeActive;
            result["target_str"] = "SingleEnemy";
            result["target"] = (int)TargetType.SingleEnemy;
        }

        result["action_cost"] = BladeHex.Data.CharacterGenerator.GetSkillApCost(name);
        result["range"] = BladeHex.Data.CharacterGenerator.GetSkillRange(name);
        result["cooldown"] = BladeHex.Data.CharacterGenerator.GetSkillCooldown(name);
        result["description"] = BladeHex.Data.CharacterGenerator.GetSkillDescription(name);
        result["mana_cost"] = 0;
        result["is_spell"] = false;

        return result;
    }
}
