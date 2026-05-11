using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 职业称号判定器 — 根据已涉足的属性组合返回职业称号
/// 纯标签层，不修改任何数值，仅用于 UI 展示
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
            { FlagInt, "法师" }, { FlagWis, "牧师" }, { FlagCha, "诗人" },

            // 15 种双属性
            { FlagStr | FlagDex, "剑舞者" }, { FlagStr | FlagCon, "重战士" },
            { FlagStr | FlagInt, "魔剑士" }, { FlagStr | FlagWis, "圣骑士" },
            { FlagStr | FlagCha, "军阀" }, { FlagDex | FlagCon, "决斗家" },
            { FlagDex | FlagInt, "秘射手" }, { FlagDex | FlagWis, "猎人" },
            { FlagDex | FlagCha, "浪客" }, { FlagCon | FlagInt, "战法师" },
            { FlagCon | FlagWis, "苦修者" }, { FlagCon | FlagCha, "铁壁将军" },
            { FlagInt | FlagWis, "贤者" }, { FlagInt | FlagCha, "术士" },
            { FlagWis | FlagCha, "神使" },

            // 20 种三属性
            { FlagStr | FlagDex | FlagCon, "武圣" },
            { FlagStr | FlagDex | FlagInt, "魔武者" },
            { FlagStr | FlagDex | FlagWis, "审判官" },
            { FlagStr | FlagDex | FlagCha, "战神" },
            { FlagStr | FlagCon | FlagInt, "铁焰魔战" },
            { FlagStr | FlagCon | FlagWis, "神殿骑士" },
            { FlagStr | FlagCon | FlagCha, "征服者" },
            { FlagStr | FlagInt | FlagWis, "天启骑士" },
            { FlagStr | FlagInt | FlagCha, "魔王" },
            { FlagStr | FlagWis | FlagCha, "圣战者" },
            { FlagDex | FlagCon | FlagInt, "影法师" },
            { FlagDex | FlagCon | FlagWis, "荒野守望" },
            { FlagDex | FlagCon | FlagCha, "千面客" },
            { FlagDex | FlagInt | FlagWis, "星辰行者" },
            { FlagDex | FlagInt | FlagCha, "幻术师" },
            { FlagDex | FlagWis | FlagCha, "风语者" },
            { FlagCon | FlagInt | FlagWis, "远古守护" },
            { FlagCon | FlagInt | FlagCha, "铁幕领主" },
            { FlagCon | FlagWis | FlagCha, "圣盾使" },
            { FlagInt | FlagWis | FlagCha, "天选者" },

            // 15 种四属性
            { FlagCon | FlagInt | FlagWis | FlagCha, "智者尊者" },
            { FlagDex | FlagInt | FlagWis | FlagCha, "灵风大师" },
            { FlagDex | FlagCon | FlagWis | FlagCha, "自然统帅" },
            { FlagDex | FlagCon | FlagInt | FlagCha, "暗影领主" },
            { FlagDex | FlagCon | FlagInt | FlagWis, "沉默之力" },
            { FlagStr | FlagInt | FlagWis | FlagCha, "毁灭之主" },
            { FlagStr | FlagCon | FlagWis | FlagCha, "铁壁圣骑" },
            { FlagStr | FlagCon | FlagInt | FlagCha, "霸道魔将" },
            { FlagStr | FlagCon | FlagInt | FlagWis, "深渊骑士" },
            { FlagStr | FlagDex | FlagWis | FlagCha, "战争之风" },
            { FlagStr | FlagDex | FlagInt | FlagCha, "狂风魔将" },
            { FlagStr | FlagDex | FlagInt | FlagWis, "独行圣者" },
            { FlagStr | FlagDex | FlagCon | FlagCha, "战争之王" },
            { FlagStr | FlagDex | FlagCon | FlagWis, "铁壁猎手" },
            { FlagStr | FlagDex | FlagCon | FlagInt, "万象魔战" },

            // 6 种五属性
            { FlagDex | FlagCon | FlagInt | FlagWis | FlagCha, "万灵使者" },
            { FlagStr | FlagCon | FlagInt | FlagWis | FlagCha, "山岳之主" },
            { FlagStr | FlagDex | FlagInt | FlagWis | FlagCha, "星界旅者" },
            { FlagStr | FlagDex | FlagCon | FlagWis | FlagCha, "自然战神" },
            { FlagStr | FlagDex | FlagCon | FlagInt | FlagCha, "铁血魔王" },
            { FlagStr | FlagDex | FlagCon | FlagInt | FlagWis, "深渊行者" },

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

        // 1. 计算各区域的大节点数量
        var regionBigCount = new Dictionary<int, int>();
        foreach (var flag in RegionToFlag.Values)
            regionBigCount[flag] = 0;

        foreach (string nodeId in skillTree.ActivatedNodes)
        {
            if (!skillTree.TreeData.Nodes.TryGetValue(nodeId, out var node)) continue;
            // 只统计大节点（BIG / KEYSTONE）的区域涉足
            if (node.CurrentNodeType == SkillNodeData.NodeType.Big ||
                node.CurrentNodeType == SkillNodeData.NodeType.Keystone)
            {
                int flag = RegionToFlag.GetValueOrDefault(node.CurrentRegion, 0);
                if (flag != 0)
                    regionBigCount[flag] = regionBigCount.GetValueOrDefault(flag, 0) + 1;
            }
        }

        // 2. 确定涉足标志
        int touchedFlags = 0;
        foreach (var kvp in regionBigCount)
        {
            if (kvp.Value >= 1)
                touchedFlags |= kvp.Key;
        }

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
            if (node.CurrentNodeType == SkillNodeData.NodeType.Big ||
                node.CurrentNodeType == SkillNodeData.NodeType.Keystone)
                rd["big"] = rd["big"].AsInt32() + 1;
            else if (node.CurrentNodeType == SkillNodeData.NodeType.Small)
                rd["small"] = rd["small"].AsInt32() + 1;
        }

        return result;
    }

    // ============================================================================
    // 内部辅助
    // ============================================================================

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
}
