using Godot;

namespace BladeHex.Strategic;

/// <summary>
/// Runtime figure metadata for a star-chart node.
/// The figure is the multi-tile shape players fill; the node is the gameplay owner.
/// </summary>
public sealed class SkillNodeFigureData
{
    public string FigureId { get; init; } = "";
    public string FigureName { get; init; } = "";
    public string TemplateId { get; init; } = "";
    public SkillNodeData.ActivationShape ActivationShape { get; init; }
    public SkillNodeData.Region Region { get; init; }
    public Vector2I[] Tiles { get; init; } = [];
    public bool IsCareerDefining { get; init; }
}
