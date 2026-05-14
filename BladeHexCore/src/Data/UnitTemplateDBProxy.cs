using Godot;

namespace BladeHex.Data;

/// <summary>
/// UnitTemplateDB 可访问代理
/// </summary>
[GlobalClass]
public partial class UnitTemplateDBProxy : RefCounted
{
    public static Godot.Collections.Array<Godot.Collections.Dictionary> GetAllTemplates()
    {
        var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var tpl in UnitTemplateDB.GetAllTemplates())
            result.Add(tpl);
        return result;
    }
}
