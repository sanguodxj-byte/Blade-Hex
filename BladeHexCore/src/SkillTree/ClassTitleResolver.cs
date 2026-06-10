using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 职业称号判定器 — 根据已完成的职业图形节点返回职业称号
/// 单片瓦片只提供属性，不参与职业判定。
/// </summary>
public static class ClassTitleResolver
{
    // ============================================================================
    // 属性标志位定义（位运算组合）
    // ============================================================================

    public const int FlagStr = 1;     // 000001
    public const int FlagDex = 2;     // 000010
    public const int FlagCon = 4;     // 000100
    public const int FlagInt = 8;     // 001000
    public const int FlagWis = 16;    // 010000
    public const int FlagCha = 32;    // 100000

    private static readonly Dictionary<SkillNodeData.Region, int> RegionToFlag = new()
    {
        { SkillNodeData.Region.Str, FlagStr },
        { SkillNodeData.Region.Dex, FlagDex },
        { SkillNodeData.Region.Con, FlagCon },
        { SkillNodeData.Region.Int, FlagInt },
        { SkillNodeData.Region.Wis, FlagWis },
        { SkillNodeData.Region.Cha, FlagCha },
    };

    private static readonly Dictionary<int, string> FlagToLabel = new()
    {
        { FlagStr, "STR" }, { FlagDex, "DEX" }, { FlagCon, "CON" },
        { FlagInt, "INT" }, { FlagWis, "WIS" }, { FlagCha, "CHA" },
    };

    private static readonly int[] FlagOrder = [FlagStr, FlagDex, FlagCon, FlagInt, FlagWis, FlagCha];

    // ============================================================================
    // 63 种职业称号查找表
    // ============================================================================

    private static Dictionary<int, string>? _titleTable;

    private static void EnsureTable()
    {
        if (_titleTable != null) return;
        _titleTable = new Dictionary<int, string>
        {
            // 6 种单属性
            { FlagStr, "战士" }, { FlagDex, "游侠" }, { FlagCon, "守卫" },
            { FlagInt, "法师" }, { FlagWis, "刺客" }, { FlagCha, "诗人" },

            // 15 种双属性
            { FlagStr | FlagDex, "剑舞者" }, { FlagStr | FlagCon, "重战士" },
            { FlagStr | FlagInt, "魔剑士" }, { FlagStr | FlagWis, "处刑人" },
            { FlagStr | FlagCha, "征讨者" }, { FlagDex | FlagCon, "决斗者" },
            { FlagDex | FlagInt, "秘射手" }, { FlagDex | FlagWis, "狩猎者" },
            { FlagDex | FlagCha, "游荡者" }, { FlagCon | FlagInt, "战法师" },
            { FlagCon | FlagWis, "苦修者" }, { FlagCon | FlagCha, "守御者" },
            { FlagInt | FlagWis, "大贤者" }, { FlagInt | FlagCha, "指引者" },
            { FlagWis | FlagCha, "预言者" },

            // 20 种三属性
            { FlagStr | FlagDex | FlagCon, "大宗师" },
            { FlagStr | FlagDex | FlagInt, "魔武者" },
            { FlagStr | FlagDex | FlagWis, "审判官" },
            { FlagStr | FlagDex | FlagCha, "战誓者" },
            { FlagStr | FlagCon | FlagInt, "述法者" },
            { FlagStr | FlagCon | FlagWis, "惩罚者" },
            { FlagStr | FlagCon | FlagCha, "征服者" },
            { FlagStr | FlagInt | FlagWis, "毁灭者" },
            { FlagStr | FlagInt | FlagCha, "支配者" },
            { FlagStr | FlagWis | FlagCha, "十字军" },
            { FlagDex | FlagCon | FlagInt, "影缄者" },
            { FlagDex | FlagCon | FlagWis, "鹰眼卫" },
            { FlagDex | FlagCon | FlagCha, "游骑兵" },
            { FlagDex | FlagInt | FlagWis, "唤星者" },
            { FlagDex | FlagInt | FlagCha, "幻术师" },
            { FlagDex | FlagWis | FlagCha, "风语者" },
            { FlagCon | FlagInt | FlagWis, "敌法师" },
            { FlagCon | FlagInt | FlagCha, "铁幕领主" },
            { FlagCon | FlagWis | FlagCha, "誓盾卫" },
            { FlagInt | FlagWis | FlagCha, "天选者" },

            // 15 种四属性
            { FlagCon | FlagInt | FlagWis | FlagCha, "秘院贤师" },
            { FlagDex | FlagInt | FlagWis | FlagCha, "灵风秘庭" },
            { FlagDex | FlagCon | FlagWis | FlagCha, "荒原之心" },
            { FlagDex | FlagCon | FlagInt | FlagCha, "血契之环" },
            { FlagDex | FlagCon | FlagInt | FlagWis, "静默之刃" },
            { FlagStr | FlagInt | FlagWis | FlagCha, "毁灭王冠" },
            { FlagStr | FlagCon | FlagWis | FlagCha, "磐石守护" },
            { FlagStr | FlagCon | FlagInt | FlagCha, "铁铸领主" },
            { FlagStr | FlagCon | FlagInt | FlagWis, "渊狱骑士" },
            { FlagStr | FlagDex | FlagWis | FlagCha, "战争之风" },
            { FlagStr | FlagDex | FlagInt | FlagCha, "焰风之怒" },
            { FlagStr | FlagDex | FlagInt | FlagWis, "孤刃之誓" },
            { FlagStr | FlagDex | FlagCon | FlagCha, "战争领主" },
            { FlagStr | FlagDex | FlagCon | FlagWis, "钢弦骑士" },
            { FlagStr | FlagDex | FlagCon | FlagInt, "鏖战骑士" },

            // 6 种五属性
            { FlagDex | FlagCon | FlagInt | FlagWis | FlagCha, "万灵之约印" },
            { FlagStr | FlagCon | FlagInt | FlagWis | FlagCha, "山岳之王座" },
            { FlagStr | FlagDex | FlagInt | FlagWis | FlagCha, "星界之裂隙" },
            { FlagStr | FlagDex | FlagCon | FlagWis | FlagCha, "荒芜之化身" },
            { FlagStr | FlagDex | FlagCon | FlagInt | FlagCha, "铁血之律令" },
            { FlagStr | FlagDex | FlagCon | FlagInt | FlagWis, "孤星之刃影" },

            // 1 种全属性
            { FlagStr | FlagDex | FlagCon | FlagInt | FlagWis | FlagCha, "万象" },
        };
    }

    // ============================================================================
    // 公共接口
    // ============================================================================

    /// <summary>
    /// 判定职业称号
    /// </summary>
    public static Godot.Collections.Dictionary Resolve(
        CharacterSkillTree skillTree,
        Godot.Collections.Dictionary? characterAttrs = null)
    {
        EnsureTable();
        characterAttrs ??= new Godot.Collections.Dictionary();

        // 1. 计算各区域已完成的职业图形节点数量。
        var regionBigCount = CountCareerDefiningNodes(skillTree);

        // 2. 确定涉足标志
        int touchedFlags = BuildTouchedFlags(regionBigCount);

        // 3. 无涉足 → 无名者
        if (touchedFlags == 0)
            return new Godot.Collections.Dictionary { { "title", "无名者" }, { "flags", 0 }, { "label", "" } };

        // 4. 查表
        string title = _titleTable!.GetValueOrDefault(touchedFlags, "无名者");
        if (string.IsNullOrEmpty(title)) title = "无名者";

        // 5. 生成显示标签
        string label = BuildLabel(touchedFlags, regionBigCount, characterAttrs);

        return new Godot.Collections.Dictionary { { "title", title }, { "flags", touchedFlags }, { "label", label } };
    }

    public static bool IsCareerDefiningNode(SkillNodeData node)
    {
        return (node.CurrentNodeType == SkillNodeData.NodeType.Big ||
                node.CurrentNodeType == SkillNodeData.NodeType.Giant ||
                node.CurrentNodeType == SkillNodeData.NodeType.Keystone)
            && GetNodeRegionFlag(node) != 0;
    }

    public static int GetNodeRegionFlag(SkillNodeData node)
    {
        return RegionToFlag.GetValueOrDefault(node.CurrentRegion, 0);
    }

    public static int GetCareerFlags(CharacterSkillTree skillTree)
    {
        return BuildTouchedFlags(CountCareerDefiningNodes(skillTree));
    }

    public static int GetCareerFlagsAfterCompleting(CharacterSkillTree skillTree, SkillNodeData node)
    {
        int flags = GetCareerFlags(skillTree);
        if (IsCareerDefiningNode(node))
            flags |= GetNodeRegionFlag(node);
        return flags;
    }

    public static string GetTitleByFlags(int flags)
    {
        EnsureTable();
        if (flags == 0) return "无名者";
        return _titleTable!.GetValueOrDefault(flags, "无名者");
    }

    /// <summary>快速获取称号名称</summary>
    public static string GetTitle(CharacterSkillTree skillTree, Godot.Collections.Dictionary? characterAttrs = null)
    {
        return Resolve(skillTree, characterAttrs)["title"].AsString();
    }

    /// <summary>获取各区域的大节点统计</summary>
    public static Godot.Collections.Dictionary GetRegionStats(CharacterSkillTree skillTree)
    {
        var result = new Godot.Collections.Dictionary
        {
            { "STR", new Godot.Collections.Dictionary { { "big", 0 }, { "small", 0 } } },
            { "DEX", new Godot.Collections.Dictionary { { "big", 0 }, { "small", 0 } } },
            { "CON", new Godot.Collections.Dictionary { { "big", 0 }, { "small", 0 } } },
            { "INT", new Godot.Collections.Dictionary { { "big", 0 }, { "small", 0 } } },
            { "WIS", new Godot.Collections.Dictionary { { "big", 0 }, { "small", 0 } } },
            { "CHA", new Godot.Collections.Dictionary { { "big", 0 }, { "small", 0 } } },
        };

        var regionNameMap = new Dictionary<SkillNodeData.Region, string>
        {
            { SkillNodeData.Region.Str, "STR" }, { SkillNodeData.Region.Dex, "DEX" },
            { SkillNodeData.Region.Con, "CON" }, { SkillNodeData.Region.Int, "INT" },
            { SkillNodeData.Region.Wis, "WIS" }, { SkillNodeData.Region.Cha, "CHA" },
        };

        foreach (string nodeId in skillTree.ActivatedNodes)
        {
            if (!skillTree.TreeData.Nodes.TryGetValue(nodeId, out var node)) continue;
            string rname = regionNameMap.GetValueOrDefault(node.CurrentRegion, "");
            if (string.IsNullOrEmpty(rname)) continue;
            var rd = (Godot.Collections.Dictionary)result[rname];
            if (IsCareerDefiningNode(node))
                rd["big"] = rd["big"].AsInt32() + 1;
            else if (node.CurrentNodeType == SkillNodeData.NodeType.Small)
                rd["small"] = rd["small"].AsInt32() + 1;
        }

        return result;
    }

    // ============================================================================
    // 内部辅助
    // ============================================================================

    private static Dictionary<int, int> CountCareerDefiningNodes(CharacterSkillTree skillTree)
    {
        var regionBigCount = new Dictionary<int, int>();
        foreach (var flag in RegionToFlag.Values)
            regionBigCount[flag] = 0;

        foreach (string nodeId in skillTree.ActivatedNodes)
        {
            if (!skillTree.TreeData.Nodes.TryGetValue(nodeId, out var node)) continue;
            if (!IsCareerDefiningNode(node)) continue;

            int flag = GetNodeRegionFlag(node);
            regionBigCount[flag] = regionBigCount.GetValueOrDefault(flag, 0) + 1;
        }

        return regionBigCount;
    }

    private static int BuildTouchedFlags(Dictionary<int, int> regionBigCount)
    {
        int touchedFlags = 0;
        foreach (var kvp in regionBigCount)
        {
            if (kvp.Value >= 1)
                touchedFlags |= kvp.Key;
        }

        return touchedFlags;
    }

    private static string BuildLabel(int touchedFlags, Dictionary<int, int> regionBigCount,
        Godot.Collections.Dictionary characterAttrs)
    {
        var touchedList = new List<int>();
        foreach (var flag in FlagOrder)
            if ((touchedFlags & flag) != 0)
                touchedList.Add(flag);

        if (touchedList.Count == 0) return "";

        // 找主属性（大节点数最多，并列取属性值最高）
        int primaryFlag = touchedList[0];
        int primaryCount = regionBigCount.GetValueOrDefault(primaryFlag, 0);

        foreach (var flag in touchedList)
        {
            int count = regionBigCount.GetValueOrDefault(flag, 0);
            if (count > primaryCount)
            {
                primaryFlag = flag;
                primaryCount = count;
            }
            else if (count == primaryCount && characterAttrs.Count > 0)
            {
                int valNew = GetAttrValue(flag, characterAttrs);
                int valCur = GetAttrValue(primaryFlag, characterAttrs);
                if (valNew > valCur)
                    primaryFlag = flag;
            }
        }

        // 构建标签：主属性在前，其余按固定顺序
        var parts = new List<string> { FlagToLabel.GetValueOrDefault(primaryFlag, "?") };
        foreach (var flag in FlagOrder)
        {
            if (flag != primaryFlag && (touchedFlags & flag) != 0)
                parts.Add(FlagToLabel.GetValueOrDefault(flag, "?"));
        }

        return string.Join("+", parts);
    }

    private static int GetAttrValue(int flag, Godot.Collections.Dictionary attrs)
    {
        return flag switch
        {
            FlagStr => attrs.ContainsKey("str") ? attrs["str"].AsInt32() : 10,
            FlagDex => attrs.ContainsKey("dex") ? attrs["dex"].AsInt32() : 10,
            FlagCon => attrs.ContainsKey("con") ? attrs["con"].AsInt32() : 10,
            FlagInt => attrs.ContainsKey("intel") ? attrs["intel"].AsInt32() : 10,
            FlagWis => attrs.ContainsKey("wis") ? attrs["wis"].AsInt32() : 10,
            FlagCha => attrs.ContainsKey("cha") ? attrs["cha"].AsInt32() : 10,
            _ => 10
        };
    }

    // ============================================================================
    // 职业图标映射
    // ============================================================================

    private static Dictionary<string, string>? _iconTable;

    private static void EnsureIconTable()
    {
        if (_iconTable != null) return;
        // 注意：每个中文 key 必须唯一，否则后写覆盖前写。
        // 仅映射到 assets/class_icons/ 中实际存在的 PNG；缺图标的称号留空表项以走 fallback。
        _iconTable = new Dictionary<string, string>
        {
            // ---- 6 单属性 ----
            { "战士", "Warrior" }, { "游侠", "Ranger" }, { "守卫", "Guardian" },
            { "法师", "Mage" }, { "刺客", "Assassin" },
            // "诗人" 暂无 Bard.png — 留空走 fallback

            // ---- 15 双属性 ----
            { "剑舞者", "BladeDancer" }, { "重战士", "Juggernaut" },
            { "魔剑士", "Spellsword" },     // 缺图标
            { "处刑人", "Executioner" }, { "征讨者", "Warlord" },
            { "决斗者", "Duelist" }, { "秘射手", "ArcaneArcher" },
            { "狩猎者", "Falconer" }, { "游荡者", "Rogue" },
            { "战法师", "Battlemage" },     // 缺图标
            { "苦修者", "Veteran" },
            { "守御者", "IronCommander" },
            { "大贤者", "Sage" },             // 缺图标
            { "指引者", "Sorcerer" },         // 缺图标
            { "预言者", "Prophet" },          // 缺图标

            // ---- 20 三属性 ----
            { "大宗师", "Grandmaster" }, { "魔武者", "Spellweaver" },
            { "审判官", "Hawkeye" }, { "战誓者", "Champion" },
            { "述法者", "Ironweaver" }, { "惩罚者", "Skullcrusher" },
            { "征服者", "Conqueror" },
            { "毁灭者", "DoomKnight" },   // 缺图标
            { "支配者", "Overlord" },
            { "十字军", "Crusader" },     // 缺图标
            { "影缄者", "ShadowMage" }, { "鹰眼卫", "Nightstalker" },
            { "游骑兵", "Outrider" }, { "唤星者", "Stargazer" }, // 缺图标
            { "幻术师", "Illusionist" }, { "风语者", "Windwalker" },
            { "敌法师", "ArcaneWarden" },
            { "铁幕领主", "IronSovereign" }, // 缺图标
            { "誓盾卫", "HolyBulwark" },
            { "天选者", "ChosenOne" },

            // ---- 15 四属性 ----
            { "秘院贤师", "Archsage" }, { "灵风秘庭", "ZephyrMaster" },
            { "荒原之心", "Warchief" },
            { "血契之环", "ShadowLord" },
            { "静默之刃", "SilentDeath" }, { "毁灭王冠", "LordOfRuin" },
            { "磐石守护", "StoneSaint" },
            { "铁铸领主", "DreadGeneral" }, // 缺图标
            { "渊狱骑士", "VoidKnight" },
            { "战争之风", "StormBanner" },
            { "焰风之怒", "TempestLord" },
            { "孤刃之誓", "LoneBlade" },
            { "战争领主", "WarKing" },
            { "钢弦骑士", "SkyHunter" },
            { "鏖战骑士", "ArcaneCalamity" },

            // ---- 6 五属性 ----
            { "万灵之约印", "Emissary" },
            { "山岳之王座", "MountainLord" }, // 缺图标
            { "星界之裂隙", "AstralWalker" },
            { "荒芜之化身", "WrathAvatar" },
            { "铁血之律令", "IronTyrant" },
            { "孤星之刃影", "LoneShadow" },

            // ---- 1 全属性 ----
            { "万象", "Paragon" },
        };
    }

    /// <summary>根据职业称号获取图标资源路径（64x64）</summary>
    /// <remarks>
    /// 仅在 ResourceLoader 能找到对应 PNG 时返回路径；否则返回 null 由 UI 走 fallback。
    /// 这避免了图标资产尚未生成时控制台抛 "Resource not found" 错误。
    /// </remarks>
    public static string? GetIconPath(string title)
    {
        if (string.IsNullOrEmpty(title) || title == "无名者") return null;
        EnsureIconTable();
        if (!_iconTable!.TryGetValue(title, out var iconName) || string.IsNullOrEmpty(iconName))
            return null;
        var path = $"res://assets/class_icons/{iconName}.png";
        return Godot.ResourceLoader.Exists(path) ? path : null;
    }
}
