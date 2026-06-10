// SkillTreeData.cs
// Fixed skill star chart data loaded from JSON.
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// Shared skill star chart graph. The layout and content source of truth is
/// <c>skill_tree_layout.json</c> plus <c>skill_tree_content.json</c>.
/// </summary>
[GlobalClass]
public partial class SkillTreeData : RefCounted
{
    public const string StartNodeId = "start";
    public const int FixedLayoutRadius = 20;

    public Dictionary<string, SkillNodeData> Nodes { get; } = new();

    public SkillTreeData()
    {
        LoadFromJson();
    }

    public int GetNodeCount() => Nodes.Count;

    public SkillNodeData? GetStartNode() =>
        Nodes.GetValueOrDefault(StartNodeId);

    private void LoadFromJson()
    {
        Nodes.Clear();

        if (!SkillTreeLayoutLoader.TryLoadDefault(Nodes, out var loadError))
        {
            Nodes.Clear();
            GD.PushError($"[SkillTree] Fixed JSON layout load failed: {loadError}.");
            return;
        }

        var (ok, validationMessage) = SkillTreeLayoutLoader.Validate(Nodes, FixedLayoutRadius);
        if (!ok)
        {
            Nodes.Clear();
            GD.PushError($"[SkillTree] Fixed JSON layout validation failed: {validationMessage}.");
            return;
        }

        GD.Print($"[SkillTree] Loaded fixed layout from JSON: nodes={Nodes.Count}");
    }
}
