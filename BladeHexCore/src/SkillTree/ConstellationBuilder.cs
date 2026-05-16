// ConstellationBuilder.cs
// 技能盘"星座式"绘制 DSL — 把节点位置和连线抽象成几何图元
// 用法示例（STR 区域，方向 dir=0）:
//   new ConstellationBuilder(0)
//       .At("str_s01", 1, 0)
//       .At("str_s02", 1, 1)
//       .At("str_b02", 2, 0)
//       .Triangle("str_s01", "str_s02", "str_b02")  // 三角形面块
//       .Stroke("str_s08", "str_s07", "str_b01", "str_s04", "str_s05") // 折线
//       .Polygon("a", "b", "c", "d") // 闭合多边形
//       .Apply(this);
//
// 坐标系约定（canonical, dirIdx=0）:
//   axial = 沿主轴前进步数（Dirs[0])
//   lateral = 沿副轴侧偏步数（Dirs[1]),正值朝一侧、负值朝另一侧
//   At(id, axial, lateral) → grid 坐标 axial*Dirs[dirIdx] + lateral*Dirs[(dirIdx+1)%6]
//
// 旋转：传入 dirIdx=k 时, 同一个 (axial, lateral) 输入会被自动旋转到 60°×k 区域
using Godot;
using System.Collections.Generic;

namespace BladeHex.Strategic;

/// <summary>
/// 技能盘"星座"DSL 构造器。
/// 为单个区域定义节点位置和连线关系，
/// 让"画图案"和"建拓扑"分离，提升可读性与可维护性。
/// </summary>
public class ConstellationBuilder
{
    private readonly int _dirIdx;
    private readonly Dictionary<string, Vector2I> _positions = new();
    private readonly List<(string, string)> _edges = new();

    /// <summary>新建一个区域构造器。</summary>
    /// <param name="dirIdx">区域方向索引 0..5（对应 STR/DEX/CON/INT/WIS/CHA）</param>
    public ConstellationBuilder(int dirIdx)
    {
        _dirIdx = ((dirIdx % 6) + 6) % 6;
    }

    /// <summary>
    /// 把 canonical 局部坐标 (axial, lateral) 转换为 grid 坐标，
    /// 受当前 dirIdx 旋转。
    /// </summary>
    private Vector2I ToGrid(int axial, int lateral)
    {
        var u = SkillTreeData.Dirs[_dirIdx];
        var v = SkillTreeData.Dirs[(_dirIdx + 1) % 6];
        return u * axial + v * lateral;
    }

    /// <summary>
    /// 在 (axial, lateral) 处放置一个节点。
    /// 如果同一 id 多次调用，最后一次胜出。
    /// </summary>
    public ConstellationBuilder At(string id, int axial, int lateral)
    {
        _positions[id] = ToGrid(axial, lateral);
        return this;
    }

    /// <summary>沿折线连接给定节点（n 个点产生 n-1 条边）。</summary>
    public ConstellationBuilder Stroke(params string[] ids)
    {
        for (int i = 0; i + 1 < ids.Length; i++)
            _edges.Add((ids[i], ids[i + 1]));
        return this;
    }

    /// <summary>三角形面块（3 条边）。等价于 Polygon(a,b,c)。</summary>
    public ConstellationBuilder Triangle(string a, string b, string c)
    {
        _edges.Add((a, b));
        _edges.Add((b, c));
        _edges.Add((c, a));
        return this;
    }

    /// <summary>闭合多边形（n 条边，最后回到起点）。</summary>
    public ConstellationBuilder Polygon(params string[] ids)
    {
        if (ids.Length < 2) return this;
        for (int i = 0; i < ids.Length; i++)
            _edges.Add((ids[i], ids[(i + 1) % ids.Length]));
        return this;
    }

    /// <summary>显式添加一条边。</summary>
    public ConstellationBuilder Edge(string a, string b)
    {
        _edges.Add((a, b));
        return this;
    }

    /// <summary>
    /// 把所有位置和边写入目标 SkillTreeData。
    /// 位置直接覆盖；边幂等地追加到双方 Neighbors。
    /// </summary>
    public void Apply(SkillTreeData target)
    {
        foreach (var (id, pos) in _positions)
        {
            if (target.Nodes.TryGetValue(id, out var node))
                node.GridPosition = pos;
        }
        foreach (var (a, b) in _edges)
            target.AddEdge(a, b);
    }

    /// <summary>读取当前 builder 中已记录的某节点 grid 坐标（用于调试或链接外部连线）。</summary>
    public bool TryGetPosition(string id, out Vector2I pos) => _positions.TryGetValue(id, out pos);
}
