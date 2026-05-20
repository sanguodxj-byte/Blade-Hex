// SkillTreeAllocator.cs
// Build a CharacterSkillTree for a UnitData and auto-allocate skill points
// based on the unit's primary stat. Same policy used by QuickCombat / sim.
//
// Lives in Core so headless simulation can call it without depending on the
// Godot autoload SkillTreeManager.
using BladeHex.Data;

namespace BladeHex.Strategic;

/// <summary>
/// Static helper that creates a fresh skill tree for a unit and runs the AI
/// allocation policy ("most points into the unit's primary attribute").
/// </summary>
public static class SkillTreeAllocator
{
    /// <summary>
    /// Create a CharacterSkillTree for <paramref name="data"/> at the given
    /// level, allocate ~5 + (level-1) points, register jump charges, then run
    /// <see cref="CharacterSkillTree.AiAllocatePoints"/> with the unit's
    /// primary + secondary attributes.
    /// </summary>
    /// <param name="data">Unit to allocate for. Must have non-null stats.</param>
    /// <param name="treeData">
    /// Shared skill-tree graph. Pass <c>null</c> to build a fresh one (cheap).
    /// In production code you should pass the cached singleton from
    /// <c>SkillTreeManager.TreeData</c> to avoid redundant allocation.
    /// </param>
    /// <param name="aiAccuracy">0..1, how accurately AI follows the priority order.</param>
    /// <returns>The configured tree, or null if data is invalid.</returns>
    public static CharacterSkillTree? AllocateForUnit(
        UnitData data,
        SkillTreeData? treeData = null,
        float aiAccuracy = 0.8f)
    {
        if (data == null) return null;

        var graph = treeData ?? new SkillTreeData();
        var tree = new CharacterSkillTree(graph, data.Level);

        int points = 5 + (data.Level - 1);
        tree.AddSkillPoint(points);

        int jumps = 1 + (data.Level - 1) / 6;
        if (jumps > 6) jumps = 6;
        for (int i = 0; i < jumps; i++)
            tree.RegisterJump();

        string primary = GetPrimaryAttr(data);
        string secondary = GetSecondaryAttr(data, primary);
        tree.AiAllocatePoints(aiAccuracy, primary, secondary);
        return tree;
    }

    /// <summary>Highest of STR / DEX / INT / WIS — lowercase for AiAllocatePoints' API.</summary>
    public static string GetPrimaryAttr(UnitData data)
    {
        if (data == null) return "str";
        int max = data.Str;
        string attr = "str";
        if (data.Dex > max)   { max = data.Dex; attr = "dex"; }
        if (data.Intel > max) { max = data.Intel; attr = "int"; }
        if (data.Wis > max)   { max = data.Wis; attr = "wis"; }
        return attr;
    }

    /// <summary>Second highest, excluding the primary.</summary>
    public static string GetSecondaryAttr(UnitData data, string primary)
    {
        if (data == null) return "dex";
        var attrs = new (string name, int val)[]
        {
            ("str", data.Str), ("dex", data.Dex), ("con", data.Con),
            ("int", data.Intel), ("wis", data.Wis), ("cha", data.Cha),
        };
        System.Array.Sort(attrs, (a, b) => b.val.CompareTo(a.val));
        return attrs[0].name == primary ? attrs[1].name : attrs[0].name;
    }
}
