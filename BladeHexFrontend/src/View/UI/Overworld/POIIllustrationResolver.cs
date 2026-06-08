// POIIllustrationResolver.cs
// POI 插图路径集中解析器 — 唯一真相源
// 消除各面板中重复的 TownType→前缀映射和 variant 选择逻辑
using Godot;
using System.Collections.Generic;
using BladeHex.View.AssetSystem;

namespace BladeHex.View.UI.Overworld;

/// <summary>
/// POI 插图路径解析器。
/// 集中管理 TownType/设施类型 → 插图文件路径的映射，统一 variant 随机选择。
/// </summary>
public static class POIIllustrationResolver
{
    // ============================================================================
    // 配置
    // ============================================================================

    private const int VariantCount = 3;
    private const string BasePath = "res://assets/generated_poi_illust";

    // ============================================================================
    // 映射表（唯一真相源）
    // ============================================================================

    /// <summary>OverworldTown.TownType → 插图文件前缀</summary>
    private static readonly Dictionary<string, string> TownTypePrefixes = new()
    {
        ["village"] = "poi_village",
        ["port"] = "poi_port",
        ["castle"] = "poi_castle",
        ["outpost"] = "poi_outpost",
        ["tavern"] = "poi_tavern",
        ["mine"] = "poi_mine",
        ["shrine"] = "poi_shrine",
        ["town"] = "poi_town",
    };

    /// <summary>设施面板类型 → 插图文件前缀</summary>
    private static readonly Dictionary<string, string> PanelPrefixes = new()
    {
        ["smithy"] = "panel_smithy",
        ["temple"] = "panel_temple",
        ["arena"] = "panel_arena",
        ["recruit"] = "panel_recruit",
        ["rest"] = "panel_rest",
        ["quest_board"] = "panel_quest_board",
        ["port"] = "panel_port",
        ["market"] = "panel_market",
    };

    /// <summary>NPC类型 → 插图文件前缀</summary>
    private static readonly Dictionary<int, string> NpcTypePrefixes = new()
    {
        [0] = "npc_adventurer",     // NpcType.Adventurer
        [1] = "npc_merchant",       // NpcType.Merchant
        [2] = "npc_traveler",       // NpcType.Traveler
        [3] = "npc_wandering_knight", // NpcType.WanderingKnight
        [4] = "npc_bounty_target",  // NpcType.BountyTarget
        [5] = "npc_hostile_humanoid", // NpcType.HostileHumanoid
    };

    // ============================================================================
    // 公共 API
    // ============================================================================

    /// <summary>按城镇类型获取插图路径（随机 variant）</summary>
    public static string? GetTownIllustration(string? townType)
    {
        if (string.IsNullOrEmpty(townType)) return null;
        if (!TownTypePrefixes.TryGetValue(townType, out var prefix))
            prefix = "poi_town";
        return BuildPath(prefix);
    }

    /// <summary>按设施面板类型获取插图路径（随机 variant）</summary>
    public static string? GetPanelIllustration(string panelType)
    {
        if (!PanelPrefixes.TryGetValue(panelType, out var prefix))
            return null;
        return BuildPath(prefix);
    }

    /// <summary>加载插图纹理，失败返回 null</summary>
    public static Texture2D? LoadIllustration(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        return TextureAssetResolver.LoadPoiIllustration(path);
    }

    /// <summary>一步到位：按城镇类型加载插图</summary>
    public static Texture2D? LoadTownIllustration(string? townType)
        => LoadIllustration(GetTownIllustration(townType));

    /// <summary>一步到位：按设施类型加载插图</summary>
    public static Texture2D? LoadPanelIllustration(string panelType)
        => LoadIllustration(GetPanelIllustration(panelType));

    /// <summary>按 NPC 类型获取插图路径（随机 variant）</summary>
    public static string? GetNpcIllustration(int npcTypeVal)
    {
        if (!NpcTypePrefixes.TryGetValue(npcTypeVal, out var prefix))
            prefix = "npc_traveler";
        return BuildPath(prefix);
    }

    /// <summary>一步到位：按 NPC 类型加载插图</summary>
    public static Texture2D? LoadNpcIllustration(int npcTypeVal)
        => LoadIllustration(GetNpcIllustration(npcTypeVal));

    // ============================================================================
    // 内部
    // ============================================================================

    private static string BuildPath(string prefix)
    {
        int variant = (int)(GD.Randi() % VariantCount) + 1;
        return $"{BasePath}/{prefix}_{variant:D2}.png";
    }
}
