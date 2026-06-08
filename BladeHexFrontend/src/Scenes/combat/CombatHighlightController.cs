// CombatHighlightController.cs
// 从 CombatSceneBase 提取的高亮控制器。
// 负责：移动范围/攻击范围/hover高亮/攻击叠加层/部署区高亮。
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Map;
using BladeHex.Combat;

namespace BladeHex.Scenes;

/// <summary>战斗场景高亮控制器。</summary>
[GlobalClass]
public partial class CombatHighlightController : Node
{
    // ===== 依赖注入 =====
    public HexGrid? HexGrid { get; set; }
    public CombatTargetingController? TargetingCtrl { get; set; }

    // ===== 内部状态 =====
    private readonly List<HexCell> _highlightedCells = new();
    public List<HexCell> HighlightedCells => _highlightedCells;
    private readonly List<HexCell> _baseRangeCells = new();
    private readonly List<HexCell> _skillRangeCells = new();
    private readonly List<HexCell> _attackRangeOverlayCells = new();

    // ===== 精细化清理方法 =====

    public void ClearBaseRange()
    {
        foreach (var cell in _baseRangeCells)
            cell?.SetHighlight(false);
        _baseRangeCells.Clear();
        SyncHighlightedCells();
    }

    public void ClearSkillRange()
    {
        foreach (var cell in _skillRangeCells)
            cell?.SetHighlight(false);
        _skillRangeCells.Clear();
        SyncHighlightedCells();
    }

    public void ClearHighlights()
    {
        ClearBaseRange();
        ClearSkillRange();
    }

    public void ClearTransientPreviews()
    {
        HideHoverOutline();
        ClearPathPreview();
        if (TargetingCtrl != null)
        {
            TargetingCtrl.ClearAoePreview(HighlightedCellsContains);
        }
    }

    public void ClearActionPreviews()
    {
        ClearSkillRange();
        ClearAttackRangeOverlay();
        ClearTransientPreviews();
    }

    private void SyncHighlightedCells()
    {
        _highlightedCells.Clear();
        _highlightedCells.AddRange(_baseRangeCells);
        _highlightedCells.AddRange(_skillRangeCells);
    }

    // ===== 通用高亮方法 =====

    public void HighlightRange(Unit unit, int range, Color color, bool emptyOnly = false)
    {
        ClearSkillRange();
        if (HexGrid == null) return;
        foreach (var coord in HexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, range))
        {
            var cell = HexGrid.GetCell(coord.X, coord.Y);
            if (cell != null && (!emptyOnly || cell.Occupant == null))
            {
                cell.SetHighlight(true, color);
                _skillRangeCells.Add(cell);
            }
        }
        SyncHighlightedCells();
    }

    public void HighlightRange(Unit unit, List<Vector2I> cells, Color color)
    {
        ClearSkillRange();
        if (HexGrid == null) return;
        foreach (var coord in cells)
        {
            var cell = HexGrid.GetCell(coord.X, coord.Y);
            if (cell != null)
            {
                cell.SetHighlight(true, color);
                _skillRangeCells.Add(cell);
            }
        }
        SyncHighlightedCells();
    }

    public bool HighlightedCellsContains(HexCell cell)
    {
        return _highlightedCells.Contains(cell) || _baseRangeCells.Contains(cell) || _skillRangeCells.Contains(cell);
    }

    public void HighlightMoveRange(Unit unit)
    {
        ClearBaseRange();
        if (HexGrid == null) return;
        float moveRange = unit.CurrentAp;
        if (moveRange <= 0) return;
        foreach (var coord in HexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, moveRange))
        {
            var cell = HexGrid.GetCell(coord.X, coord.Y);
            if (cell == null || cell.Occupant != null) continue;
            // 不可通行的格子不显示绿色高亮
            if (cell.Data != null && !cell.Data.isPassable) continue;
            cell.SetHighlight(true, new Color(0.0f, 0.8f, 0.0f, 0.2f), true);
            _baseRangeCells.Add(cell);
        }
        SyncHighlightedCells();
    }

    public void HighlightAttackRange(Unit unit)
    {
        ClearBaseRange();
        if (HexGrid == null) return;
        int atkRange = unit.GetWeaponRange();
        var attackerCell = HexGrid.GetCell(unit.GridPos.X, unit.GridPos.Y);

        // 攻击范围用 axial distance（直线距离），不受地形通行性影响
        foreach (var kvp in HexGrid.Cells)
        {
            int dist = HexUtils.AxialDistance(unit.GridPos, kvp.Key);
            if (dist > 0 && dist <= atkRange)
            {
                var cell = kvp.Value;
                if (cell != null)
                {
                    if (CombatAttackRules.IsMeleeWeaponAttack(unit)
                        && Mathf.Abs((attackerCell?.Elevation ?? cell.Elevation) - cell.Elevation) >= CombatAttackRules.MeleeElevationBlockThreshold)
                    {
                        continue;
                    }

                    cell.SetHighlight(true, new Color(1.0f, 0.2f, 0.2f, 0.2f), true);
                    _baseRangeCells.Add(cell);
                }
            }
        }
        SyncHighlightedCells();
    }

    public void ShowSelectedUnitHighlights(Unit? activeUnit)
    {
        if (activeUnit == null || !GodotObject.IsInstanceValid(activeUnit))
        {
            HideSelectedUnitMarker();
            return;
        }

        ShowSelectedUnitMarker(activeUnit);
        ClearBaseRange();
        if (activeUnit.CurrentAp >= 1)
            HighlightMoveRange(activeUnit);
    }

    // ===== 攻击范围叠加层（hover 预览） =====

    public void ShowAttackRangeOverlay(Unit unit)
    {
        ClearAttackRangeOverlay();
        if (HexGrid == null) return;
        int atkRange = unit.GetWeaponRange();
        var attackerCell = HexGrid.GetCell(unit.GridPos.X, unit.GridPos.Y);
        foreach (var kvp in HexGrid.Cells)
        {
            int dist = HexUtils.AxialDistance(unit.GridPos, kvp.Key);
            if (dist > 0 && dist <= atkRange)
            {
                var cell = kvp.Value;
                if (cell == null || _highlightedCells.Contains(cell) || _baseRangeCells.Contains(cell)) continue;
                if (CombatAttackRules.IsMeleeWeaponAttack(unit)
                    && Mathf.Abs((attackerCell?.Elevation ?? cell.Elevation) - cell.Elevation) >= CombatAttackRules.MeleeElevationBlockThreshold)
                {
                    continue;
                }
                cell.SetHighlight(true, new Color(1.0f, 0.3f, 0.2f, 0.2f), true);
                _attackRangeOverlayCells.Add(cell);
            }
        }
    }

    public void ClearAttackRangeOverlay()
    {
        foreach (var cell in _attackRangeOverlayCells)
        {
            if (!_highlightedCells.Contains(cell))
                cell.SetHighlight(false);
        }
        _attackRangeOverlayCells.Clear();
    }

    // ===== 路径预览线 =====
    private MeshInstance3D? _pathPreviewLine;
    private List<Vector2I>? _previewPath;

    // ===== 悬浮高亮 =====
    private HexCell? _currentHoverCell;
    private Decal? _hoverDecal;

    // ===== 当前行动单位标记 =====
    private HexCell? _selectedUnitCell;
    private Unit? _selectedUnit;
    private Decal? _selectedUnitDecal;

    public void DrawPathPreview(List<Vector2I> path, Vector3 startCellPos)
    {
        ClearPathPreview();
        if (HexGrid == null) return;

        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);

        float uiY = BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + BladeHex.View.Combat.CombatLayerHeight.UIHintLayer;

        // 起点(当前单位位置)
        mesh.SurfaceAddVertex(startCellPos + Vector3.Up * uiY);

        // 路径各点
        foreach (var coord in path)
        {
            var c = HexGrid.GetCell(coord.X, coord.Y);
            if (c != null)
                mesh.SurfaceAddVertex(c.Position + Vector3.Up * uiY);
        }

        mesh.SurfaceEnd();

        if (_pathPreviewLine == null)
        {
            _pathPreviewLine = new MeshInstance3D();
            _pathPreviewLine.Name = "PathPreviewLine";
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(0.3f, 0.9f, 0.4f, 0.8f);
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.NoDepthTest = true;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _pathPreviewLine.MaterialOverride = mat;
            AddChild(_pathPreviewLine);
        }
        _pathPreviewLine.Mesh = mesh;
        _pathPreviewLine.Visible = true;
        _previewPath = path;
    }

    public void ClearPathPreview()
    {
        _previewPath = null;
        if (_pathPreviewLine != null)
            _pathPreviewLine.Visible = false;
    }

    // ===== 悬浮六边形轮廓 =====

    public void ShowHoverOutline(HexCell cell)
    {
        if (_currentHoverCell == cell) return;
        HideHoverOutline();
        _currentHoverCell = cell;

        if (_hoverDecal == null)
        {
            _hoverDecal = new Decal();
            _hoverDecal.Name = "HoverDecal";

            var tex = BladeHex.View.Combat.HexHoverTextureGenerator.Get();
            _hoverDecal.TextureAlbedo = tex;
            _hoverDecal.TextureEmission = tex;

            float hexDiameter = HexUtils.Size * 2.0f;
            _hoverDecal.Size = new Vector3(hexDiameter, 80f, hexDiameter);

            _hoverDecal.Modulate = new Color(1.0f, 0.8f, 0.35f, 0.45f);
            _hoverDecal.EmissionEnergy = 0.8f;
            _hoverDecal.AlbedoMix = 0.3f;
            _hoverDecal.UpperFade = 0.0f;
            _hoverDecal.LowerFade = 0.0f;
            _hoverDecal.TextureNormal = null;
            _hoverDecal.RotationDegrees = new Vector3(0, 30, 0);

            AddChild(_hoverDecal);
        }

        // Decal 位置：在 hex 正上方，向下投射覆盖顶面和纹理层
        float decalY = cell.Position.Y + BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + 30f;
        _hoverDecal.Position = new Vector3(cell.Position.X, decalY, cell.Position.Z);
        _hoverDecal.Visible = true;
    }

    public void HideHoverOutline()
    {
        _currentHoverCell = null;
        if (_hoverDecal != null)
            _hoverDecal.Visible = false;
    }

    public void HideSelectedUnitMarker()
    {
        _selectedUnitCell = null;
        _selectedUnit = null;
        if (_selectedUnitDecal != null && GodotObject.IsInstanceValid(_selectedUnitDecal))
        {
            if (_selectedUnitDecal.GetParent() != this)
            {
                _selectedUnitDecal.GetParent()?.RemoveChild(_selectedUnitDecal);
                AddChild(_selectedUnitDecal);
            }
            _selectedUnitDecal.Visible = false;
        }
    }

    private void ShowSelectedUnitMarker(Unit activeUnit)
    {
        if (HexGrid == null) return;

        var cell = HexGrid.GetCell(activeUnit.GridPos.X, activeUnit.GridPos.Y);
        if (cell == null) return;
        _selectedUnitCell = cell;
        _selectedUnit = activeUnit;

        if (_selectedUnitDecal == null || !GodotObject.IsInstanceValid(_selectedUnitDecal))
        {
            _selectedUnitDecal = new Decal();
            _selectedUnitDecal.Name = "SelectedUnitDecal";

            var tex = BladeHex.View.Combat.HexHoverTextureGenerator.Get();
            _selectedUnitDecal.TextureAlbedo = tex;
            _selectedUnitDecal.TextureEmission = tex;

            float hexDiameter = HexUtils.Size * 2.18f;
            _selectedUnitDecal.Size = new Vector3(hexDiameter, 80f, hexDiameter);
            _selectedUnitDecal.Modulate = new Color(1.0f, 0.86f, 0.28f, 1.0f);
            _selectedUnitDecal.EmissionEnergy = 1.25f;
            _selectedUnitDecal.AlbedoMix = 0.25f;
            _selectedUnitDecal.UpperFade = 0.0f;
            _selectedUnitDecal.LowerFade = 0.0f;
            _selectedUnitDecal.TextureNormal = null;
            _selectedUnitDecal.RotationDegrees = new Vector3(0, 30, 0);
        }

        AttachSelectedUnitMarker(activeUnit);
        _selectedUnitDecal.Visible = true;
    }

    private void AttachSelectedUnitMarker(Unit activeUnit)
    {
        if (_selectedUnitDecal == null || !GodotObject.IsInstanceValid(_selectedUnitDecal)) return;

        if (_selectedUnitDecal.GetParent() != activeUnit)
        {
            _selectedUnitDecal.GetParent()?.RemoveChild(_selectedUnitDecal);
            activeUnit.AddChild(_selectedUnitDecal);
        }

        _selectedUnitDecal.TopLevel = false;
        float localGroundY = 32f - BladeHex.View.Combat.CombatLayerHeight.CharacterLayer;
        _selectedUnitDecal.Position = new Vector3(0f, localGroundY, 0f);
    }

    private void UpdateHoverPulse()
    {
        if (_hoverDecal == null || !_hoverDecal.Visible) return;
        float t = (float)Time.GetTicksMsec() / 1000f;
        float pulse = 0.7f + 0.3f * Mathf.Sin(t * 2.5f);
        _hoverDecal.Modulate = new Color(1.0f, 0.8f, 0.35f, pulse * 0.45f);
        _hoverDecal.EmissionEnergy = 0.5f + 0.3f * Mathf.Sin(t * 2.5f);
    }

    private void UpdateSelectedUnitMarker(double delta)
    {
        if (_selectedUnitDecal == null || !GodotObject.IsInstanceValid(_selectedUnitDecal) || !_selectedUnitDecal.Visible) return;
        if (_selectedUnit == null || !GodotObject.IsInstanceValid(_selectedUnit))
        {
            HideSelectedUnitMarker();
            return;
        }

        float t = (float)Time.GetTicksMsec() / 1000f;
        float pulse = 0.88f + 0.12f * Mathf.Sin(t * 3.0f);
        var rot = _selectedUnitDecal.RotationDegrees;
        rot.Y += 42.0f * (float)delta;
        _selectedUnitDecal.RotationDegrees = rot;
        _selectedUnitDecal.Modulate = new Color(1.0f, 0.86f, 0.28f, pulse);
        _selectedUnitDecal.EmissionEnergy = 1.0f + 0.25f * pulse;

        AttachSelectedUnitMarker(_selectedUnit);
    }

    public override void _Process(double delta)
    {
        UpdateHoverPulse();
        UpdateSelectedUnitMarker(delta);
    }

    // ===== 信号订阅与转发 =====

    public event Action<HexCell>? CellHovered;
    public event Action<HexCell>? CellHoverExited;

    public void OnCellHover(HexCell cell)
    {
        ShowHoverOutline(cell);
        CellHovered?.Invoke(cell);
    }

    public void OnCellHoverExit(HexCell cell)
    {
        HideHoverOutline();
        CellHoverExited?.Invoke(cell);
    }

    public void RegisterCell(HexCell cell)
    {
        if (cell == null) return;
        cell.CellMouseEntered += OnCellHover;
        cell.CellMouseExited += OnCellHoverExit;
    }
}
