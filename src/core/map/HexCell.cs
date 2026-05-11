using Godot;
using System;
using BladeHex.Data;
using BladeHex.Combat;

namespace BladeHex.Map;

/// <summary>
/// 战术网格中单个六边形的可视化与交互类 (HD-2D 3D版本)
/// 迁移自 GDScript HexCell.gd
/// </summary>
[GlobalClass]
public partial class HexCell : Area3D
{
    [Export] public BattleCellData? Data { get; set; }

    public Vector2I GridPos { get; set; } // 轴向坐标 (q, r)
    public int Elevation { get; set; } = 1; // 高程等级: 0低地, 1平地, 2高地
    public int CoverType { get; set; } = 0; // 掩体等级: 0无, 1半掩体(森林/树木), 2全掩体(巨石/墙壁)
    public Unit? Occupant { get; set; }

    // 可视化节点
    private MeshInstance3D? _meshInstance;
    private MeshInstance3D? _highlightMesh;
    private MeshInstance3D? _coverMeshInstance;

    private Color _baseAlbedoColor;

    [Signal] public delegate void CellSingleClickedEventHandler(HexCell cell);
    [Signal] public delegate void CellDoubleClickedEventHandler(HexCell cell);
    [Signal] public delegate void CellRightClickedEventHandler(HexCell cell);
    [Signal] public delegate void CellMouseEnteredEventHandler(HexCell cell);
    [Signal] public delegate void CellMouseExitedEventHandler(HexCell cell);

    public override void _Ready()
    {
        SetupVisuals();
        InputEvent += OnInputEvent;
        MouseEntered += () => EmitSignal(SignalName.CellMouseEntered, this);
        MouseExited += () => EmitSignal(SignalName.CellMouseExited, this);
    }

    private void SetupVisuals()
    {
        float hexRadius = HexUtils.Size;
        float hexHeight = HexUtils.Size * 0.5f;

        // 创建用于点击碰撞的形状
        var collisionShape = new CollisionShape3D();
        var cylShape = new CylinderShape3D();
        cylShape.Radius = hexRadius * 0.95f;
        cylShape.Height = hexHeight;
        collisionShape.Shape = cylShape;
        collisionShape.RotationDegrees = new Vector3(0, 30, 0); // 旋转 30 度变为平顶
        AddChild(collisionShape);

        // 创建基础六棱柱绘制
        _meshInstance = new MeshInstance3D();
        var mesh = new CylinderMesh();
        mesh.RadialSegments = 6;
        mesh.Rings = 1;
        mesh.TopRadius = hexRadius;
        mesh.BottomRadius = hexRadius;
        mesh.Height = hexHeight;
        _meshInstance.Mesh = mesh;
        _meshInstance.RotationDegrees = new Vector3(0, 30, 0);

        StandardMaterial3D material;
        if (Data != null)
        {
            // TODO: 集成 CombatMaterialManager
            // material = CombatMaterialManager.Instance.GetMaterial(Data.TerrainType, Elevation).Duplicate() as StandardMaterial3D;
            material = new StandardMaterial3D();
            material.AlbedoColor = new Color(0.5f, 0.5f, 0.5f);
        }
        else
        {
            material = new StandardMaterial3D();
            if (Elevation == 0) material.AlbedoColor = new Color(0.3f, 0.3f, 0.3f);
            else if (Elevation == 2) material.AlbedoColor = new Color(0.7f, 0.7f, 0.7f);
            else material.AlbedoColor = new Color(0.5f, 0.5f, 0.5f);
        }

        _baseAlbedoColor = material.AlbedoColor;
        _meshInstance.MaterialOverride = material;
        AddChild(_meshInstance);

        // 生成掩体视觉效果
        if (CoverType > 0)
        {
            _coverMeshInstance = new MeshInstance3D();
            var cMesh = new BoxMesh();
            var cMat = new StandardMaterial3D();
            if (CoverType == 1)
            {
                cMesh.Size = new Vector3(hexRadius * 0.6f, hexHeight * 1.5f, hexRadius * 0.6f);
                cMat.AlbedoColor = new Color(0.2f, 0.5f, 0.2f);
            }
            else
            {
                cMesh.Size = new Vector3(hexRadius * 0.8f, hexHeight * 3.0f, hexRadius * 0.8f);
                cMat.AlbedoColor = new Color(0.4f, 0.4f, 0.4f);
            }

            _coverMeshInstance.Mesh = cMesh;
            _coverMeshInstance.MaterialOverride = cMat;
            _coverMeshInstance.Position = new Vector3(0, hexHeight / 2.0f + cMesh.Size.Y / 2.0f, 0);
            AddChild(_coverMeshInstance);
        }

        // 创建高亮多边形
        _highlightMesh = new MeshInstance3D();
        var hlMesh = mesh.Duplicate() as CylinderMesh;
        if (hlMesh != null)
        {
            hlMesh.TopRadius = hexRadius * 1.05f;
            hlMesh.BottomRadius = hexRadius * 1.05f;
            hlMesh.Height = hexHeight * 1.05f;
            _highlightMesh.Mesh = hlMesh;
        }
        _highlightMesh.RotationDegrees = new Vector3(0, 30, 0);

        var hlMat = new StandardMaterial3D();
        hlMat.AlbedoColor = new Color(1, 1, 1, 0.3f);
        hlMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        hlMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _highlightMesh.MaterialOverride = hlMat;
        _highlightMesh.Visible = false;
        AddChild(_highlightMesh);
    }

    public void SetHighlight(bool active, Color? color = null)
    {
        if (_highlightMesh?.MaterialOverride is StandardMaterial3D mat)
        {
            mat.AlbedoColor = color ?? new Color(1, 1, 1, 0.3f);
        }
        if (_highlightMesh != null) _highlightMesh.Visible = active;
    }

    public void SetShrouded(bool isShrouded)
    {
        if (_meshInstance?.MaterialOverride is StandardMaterial3D mat)
        {
            if (isShrouded)
            {
                mat.AlbedoColor = _baseAlbedoColor.Darkened(0.8f);
                if (_coverMeshInstance?.MaterialOverride is StandardMaterial3D cMat)
                    cMat.AlbedoColor = cMat.AlbedoColor.Darkened(0.8f);
            }
            else
            {
                mat.AlbedoColor = _baseAlbedoColor;
                if (_coverMeshInstance?.MaterialOverride is StandardMaterial3D cMat)
                {
                    if (CoverType == 1) cMat.AlbedoColor = new Color(0.2f, 0.5f, 0.2f);
                    else cMat.AlbedoColor = new Color(0.4f, 0.4f, 0.4f);
                }
            }
        }
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 eventPosition, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                if (mouseEvent.DoubleClick) EmitSignal(SignalName.CellDoubleClicked, this);
                else EmitSignal(SignalName.CellSingleClicked, this);
            }
            else if (mouseEvent.ButtonIndex == MouseButton.Right)
            {
                EmitSignal(SignalName.CellRightClicked, this);
            }
        }
    }
}
