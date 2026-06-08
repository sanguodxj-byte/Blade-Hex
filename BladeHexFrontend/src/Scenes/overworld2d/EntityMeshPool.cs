using Godot;
using System.Collections.Generic;

namespace BladeHex.Scenes.Overworld;

/// <summary>
/// 视觉节点对象池，用于缓存和重用大地图上的 3D 实体 Node3D 节点，减少高频 GC 和卡顿。
/// </summary>
public class EntityMeshPool
{
    private readonly Stack<Node3D> _idle = new();

    /// <summary>
    /// 获取一个可用的视觉节点，如果没有则自动创建并添加为 parent 的子节点
    /// </summary>
    public Node3D Acquire(Node3D parent)
    {
        Node3D node;
        if (_idle.Count > 0)
        {
            node = _idle.Pop();
            node.Show();
        }
        else
        {
            node = CreateNewNode();
            parent.AddChild(node);
        }
        return node;
    }

    /// <summary>
    /// 归还视觉节点到空闲池，并将其设置为隐藏
    /// </summary>
    public void Release(Node3D node)
    {
        node.Hide();
        _idle.Push(node);
    }

    /// <summary>
    /// 实例化一个全新的 Node3D 实体视觉骨架
    /// </summary>
    private Node3D CreateNewNode()
    {
        var container = new Node3D();

        var mesh = new MeshInstance3D();
        mesh.Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
        var mat = new StandardMaterial3D();
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mesh.MaterialOverride = mat;
        mesh.Name = "SphereMesh";
        container.AddChild(mesh);

        var label = new Label3D();
        label.FontSize = 72;
        label.PixelSize = 0.01f;
        label.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        label.Position = new Vector3(0, 0.6f, 0);
        label.OutlineModulate = new Color(0.0f, 0.0f, 0.0f);
        label.OutlineSize = 14;
        label.NoDepthTest = true;
        label.RenderPriority = 100;
        label.Name = "Label3D";
        container.AddChild(label);

        return container;
    }

    /// <summary>
    /// 清空池，切断所有强引用
    /// </summary>
    public void Clear()
    {
        _idle.Clear();
    }
}
