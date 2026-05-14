// SkillUtils.cs
// 技能系统共享工具方法
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace BladeHex.Combat.Skills;

/// <summary>技能 handler 共享的工具方法</summary>
public static class SkillUtils
{
    /// <summary>在单位列表中查找占据指定格子的单位</summary>
    public static Unit? FindUnitAt(Vector2I pos, IEnumerable<Unit> units)
        => units.FirstOrDefault(u => u.GridPos == pos);

    /// <summary>标记技能执行失败</summary>
    public static void Fail(Godot.Collections.Dictionary result, string reason)
    {
        result["success"] = false;
        result["reason"] = reason;
    }
}
