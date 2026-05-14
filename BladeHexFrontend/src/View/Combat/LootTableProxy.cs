// LootTableProxy.cs
// RefCounted wrapper
using Godot;
using BladeHex.Data;

namespace BladeHex.Combat;

[GlobalClass]
public partial class LootTableProxy : RefCounted
{
    public static Godot.Collections.Array GenerateLoot(UnitData enemyData)
    {
        var list = LootTable.GenerateLoot(enemyData);
        var arr = new Godot.Collections.Array();
        foreach (var item in list)
            arr.Add(item);
        return arr;
    }
}
