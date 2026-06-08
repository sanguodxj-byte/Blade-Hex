using Godot;
using System;
using BladeHex.Data;
using BladeHex.Combat;
using BladeHex.View.Map;

namespace BladeHex.Map;

/// <summary>
/// 战术网格中单个六边形的可视化与交互类 (HD-2D 3D版本)
/// 视觉渲染委托给 HexCellMultiMeshBatcher 合批处理（T-701）
/// </summary>
[GlobalClass]
public partial class HexCell : Area3D
{
    [Export] public BattleCellData? Data { get; set; }

    public Vector2I GridPos { get; set; } // 轴向坐标 (q, r)
    public int Elevation { get; set; } = 1; // 高程等级: 0低地, 1平地, 2高地
    public int CoverType { get; set; } = 0; // 掩体等级: 0无, 1半掩体(森林/树木), 2全掩体(巨石/墙壁)
    public Unit? Occupant { get; set; }

    /// <summary>引用合批管理器，由 HexGrid 在创建时设置</summary>
    public HexCellMultiMeshBatcher? Batcher { get; set; }

    // 可视化节点（仅保留单元锚点和掩体，六棱柱由 Batcher 合批渲染）
    private MeshInstance3D? _coverMeshInstance;
    public Node3D? UnitAnchor { get; private set; }

    [Signal] public delegate void CellSingleClickedEventHandler(HexCell cell);
    [Signal] public delegate void CellDoubleClickedEventHandler(HexCell cell);
    [Signal] public delegate void CellRightClickedEventHandler(HexCell cell);
    [Signal] public delegate void CellMouseEnteredEventHandler(HexCell cell);
    [Signal] public delegate void CellMouseExitedEventHandler(HexCell cell);

    public override void _Ready()
    {
        SetupCollisionAndAnchor();
        SetupCoverVisual();
        RegisterWithBatcher();

        InputEvent += OnInputEvent;
        MouseEntered += () => EmitSignal(SignalName.CellMouseEntered, this);
        MouseExited += () => EmitSignal(SignalName.CellMouseExited, this);
    }

    public override void _ExitTree()
    {
        Batcher?.UnregisterCell(this);
    }

    /// <summary>创建点击碰撞形状和 UnitAnchor（不创建个体六棱柱网格）</summary>
    private void SetupCollisionAndAnchor()
    {
        float hexRadius = HexUtils.Size;
        float hexHeight = HexUtils.Size * 0.5f;

        // 创建用于点击碰撞的形状
        var collisionShape = new CollisionShape3D();
        var cylShape = new CylinderShape3D();
        cylShape.Radius = hexRadius * 0.95f;
        cylShape.Height = hexHeight;
        collisionShape.Shape = cylShape;
        collisionShape.RotationDegrees = new Vector3(0, 30, 0);
        AddChild(collisionShape);

        // UnitAnchor：Y 偏移到六棱柱顶部，Unit 作为此节点的子节点放置
        UnitAnchor = new Node3D();
        UnitAnchor.Name = "UnitAnchor";
        UnitAnchor.Position = new Vector3(0, hexHeight / 2.0f, 0);
        AddChild(UnitAnchor);
    }

    /// <summary>生成掩体视觉效果（每个格子独立，不参与合批）</summary>
    private void SetupCoverVisual()
    {
        if (CoverType <= 0) return;

        float hexRadius = HexUtils.Size;
        float hexHeight = HexUtils.Size * 0.5f;

        _coverMeshInstance = new MeshInstance3D();
        var cMesh = new BoxMesh();
        var cMat = CombatMaterialManager.Instance.GetCoverMaterial(CoverType);

        // 掩体尺寸缩小一档,避免遮挡角色 — 角色身高 ~120 单位、PixelSize=2 整体 ~120 显示高度。
        // 半掩体 ≈ 角色腰部(36 单位高);全掩体 ≈ 角色胸部(60 单位高)。
        if (CoverType == 1)
        {
            // 半掩体:小盒 — 占地 ≈ 1/3 hex,高 36
            cMesh.Size = new Vector3(hexRadius * 0.35f, 36f, hexRadius * 0.35f);
        }
        else
        {
            // 全掩体:中盒 — 占地 ≈ 1/2 hex,高 60
            cMesh.Size = new Vector3(hexRadius * 0.5f, 60f, hexRadius * 0.5f);
        }

        _coverMeshInstance.Mesh = cMesh;
        _coverMeshInstance.MaterialOverride = cMat;
        _coverMeshInstance.Position = new Vector3(0, hexHeight / 2.0f + cMesh.Size.Y / 2.0f, 0);
        AddChild(_coverMeshInstance);
    }

    /// <summary>注册到 Batcher 进行合批渲染</summary>
    private void RegisterWithBatcher()
    {
        if (Batcher == null) return;

        var terrainType = Data?.terrainType ?? BattleCellData.TerrainType.Plains;
        Batcher.RegisterCell(this, terrainType, Elevation, Position);
    }

    /// <summary>地形变更后刷新视觉效果（重新注册到 Batcher）</summary>
    public void RefreshTerrainVisual()
    {
        if (Batcher == null) return;
        var terrainType = Data?.terrainType ?? BattleCellData.TerrainType.Plains;
        Batcher.UnregisterCell(this);
        Batcher.RegisterCell(this, terrainType, Elevation, Position);
    }

    /// <summary>设置高亮状态 — 委托给 Batcher（fallback 为无操作）</summary>
    public void SetHighlight(bool active, Color? color = null, bool isSolid = false)
    {
        Batcher?.SetCellHighlight(this, active, color, isSolid);
    }

    /// <summary>设置迷雾遮蔽状态 — 委托给 Batcher</summary>
    public void SetShrouded(bool isShrouded)
    {
        Batcher?.SetCellShrouded(this, isShrouded);
    }

    private void OnInputEvent(Node camera, InputEvent @event, Vector3 eventPosition, Vector3 normal, long shapeIdx)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                GD.Print($"[HexCell] left click at ({GridPos.X},{GridPos.Y}) occupant={Occupant?.Data?.UnitName ?? "null"}");
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
