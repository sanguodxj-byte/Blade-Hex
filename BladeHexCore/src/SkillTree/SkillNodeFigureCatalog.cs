namespace BladeHex.Strategic;

/// <summary>
/// Central naming and template catalog for star-chart figures.
/// Nodes can override FigureId / FigureName / FigureTemplate, otherwise these defaults apply.
/// </summary>
public static class SkillNodeFigureCatalog
{
    public static string GetDefaultFigureId(SkillNodeData node)
    {
        string region = node.CurrentRegion.ToString().ToLowerInvariant();
        string role = node.GetActivationShape() switch
        {
            SkillNodeData.ActivationShape.Start => "start",
            SkillNodeData.ActivationShape.Attribute => "attribute",
            SkillNodeData.ActivationShape.PassiveSkill => "passive",
            SkillNodeData.ActivationShape.ActiveSkill => "active",
            SkillNodeData.ActivationShape.Keystone => "keystone",
            SkillNodeData.ActivationShape.Apex => "apex",
            _ => "figure",
        };

        return $"{node.NodeId}_{region}_{role}";
    }

    public static string GetDefaultFigureName(SkillNodeData node)
    {
        return node.GetActivationShape() switch
        {
            SkillNodeData.ActivationShape.Start => "启程星核",
            SkillNodeData.ActivationShape.Attribute => $"{node.GetRegionDisplayName()}星纹",
            SkillNodeData.ActivationShape.PassiveSkill => $"{node.NodeName}命座",
            SkillNodeData.ActivationShape.ActiveSkill => $"{node.NodeName}命座",
            SkillNodeData.ActivationShape.Keystone => $"{node.NodeName}大命座",
            SkillNodeData.ActivationShape.Apex => $"{node.NodeName}巨型命座",
            _ => $"{node.NodeName}星纹",
        };
    }

    public static string GetDefaultTemplateId(SkillNodeData node)
    {
        return node.GetActivationShape() switch
        {
            SkillNodeData.ActivationShape.Start => "start_core_6",
            SkillNodeData.ActivationShape.Attribute when node.CurrentNodeType == SkillNodeData.NodeType.Pip => "pip_1",
            SkillNodeData.ActivationShape.Attribute => "attribute_pair_2",
            SkillNodeData.ActivationShape.PassiveSkill => "passive_triangle_4",
            SkillNodeData.ActivationShape.ActiveSkill => "active_kite_4",
            SkillNodeData.ActivationShape.Keystone => "keystone_crown_6",
            SkillNodeData.ActivationShape.Apex => "apex_rune_12",
            _ => "attribute_pair_2",
        };
    }
}
