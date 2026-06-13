// CombatHighlightController.cs
// 从 CombatSceneBase 提取的高亮控制器。
// 负责：移动范围/攻击范围/hover高亮/攻击叠加层/部署区高亮。
using Godot;
using System;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;
using BladeHex.Combat;
using BladeHex.View.Combat;

namespace BladeHex.Scenes;

/// <summary>战斗场景高亮控制器。</summary>
[GlobalClass]
public partial class CombatHighlightController : Node
{
    // ===== 依赖注入 =====
    public HexGrid? HexGrid { get; set; }
    public CombatTargetingController? TargetingCtrl { get; set; }
    public float CurrentHour { get; set; } = 12f;

    // ===== 内部状态 =====
    private readonly List<HexCell> _highlightedCells = new();
    public List<HexCell> HighlightedCells => _highlightedCells;
    private readonly List<HexCell> _baseRangeCells = new();
    private readonly List<HexCell> _skillRangeCells = new();
    private readonly List<HexCell> _attackRangeOverlayCells = new();
    private MeshInstance3D? _moveRangeFillMesh;
    private MeshInstance3D? _moveRangeBorderMesh;
    private MeshInstance3D? _moveRangeGlowMesh;

    // ===== 精细化清理方法 =====

    public void ClearBaseRange()
    {
        foreach (var cell in _baseRangeCells)
            cell?.SetHighlight(false);
        ClearMoveRangeVisuals();
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
        var reachableCells = new List<HexCell>();
        foreach (var coord in HexGrid.GetCellsInRange(unit.GridPos.X, unit.GridPos.Y, moveRange))
        {
            var cell = HexGrid.GetCell(coord.X, coord.Y);
            if (cell == null || cell.Occupant != null) continue;
            // 不可通行的格子不显示绿色高亮
            if (cell.Data != null && !cell.Data.isPassable) continue;
            _baseRangeCells.Add(cell);
            reachableCells.Add(cell);
        }
        DrawMoveRangeVisuals(unit, reachableCells);
        SyncHighlightedCells();
    }

    private void DrawMoveRangeVisuals(Unit unit, List<HexCell> reachableCells)
    {
        if (HexGrid == null || reachableCells.Count == 0) return;

        var reachable = new HashSet<Vector2I>();
        foreach (var cell in reachableCells)
            reachable.Add(cell.GridPos);

        DrawMoveRangeField(reachableCells);
        DrawMoveRangeBoundary(reachable);
    }

    private void ClearMoveRangeVisuals()
    {
        if (_moveRangeFillMesh != null && GodotObject.IsInstanceValid(_moveRangeFillMesh))
            _moveRangeFillMesh.QueueFree();
        if (_moveRangeBorderMesh != null && GodotObject.IsInstanceValid(_moveRangeBorderMesh))
            _moveRangeBorderMesh.QueueFree();
        if (_moveRangeGlowMesh != null && GodotObject.IsInstanceValid(_moveRangeGlowMesh))
            _moveRangeGlowMesh.QueueFree();

        _moveRangeFillMesh = null;
        _moveRangeBorderMesh = null;
        _moveRangeGlowMesh = null;
    }

    private void DrawMoveRangeField(List<HexCell> reachableCells)
    {
        if (HexGrid == null || reachableCells.Count == 0) return;

        var field = new ImmediateMesh();
        field.SurfaceBegin(Mesh.PrimitiveType.Triangles);

        bool hasGeometry = false;
        float yOffset = CombatLayerHeight.HexTopOffset + CombatLayerHeight.OverlayLayer + 0.15f;

        foreach (var cell in reachableCells)
        {
            if (cell == null || !GodotObject.IsInstanceValid(cell)) continue;

            var centerColor = AdaptMoveColor(new Color(0.34f, 0.28f, 0.14f, 0.22f), cell);
            var edgeColor = AdaptMoveColor(new Color(0.18f, 0.14f, 0.07f, 0.055f), cell);
            AddMoveRangeCellWash(field, cell, yOffset, centerColor, edgeColor);
            hasGeometry = true;
        }

        field.SurfaceEnd();
        if (!hasGeometry) return;

        _moveRangeFillMesh = new MeshInstance3D
        {
            Name = "MoveRangeField",
            Mesh = field,
            MaterialOverride = CreateMoveVertexMaterial(4),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_moveRangeFillMesh);
    }

    private static void AddMoveRangeCellWash(ImmediateMesh mesh, HexCell cell, float yOffset, Color centerColor, Color edgeColor)
    {
        float apothem = HexUtils.Size * Mathf.Sqrt(3.0f) * 0.5f * 0.92f;
        float halfEdge = HexUtils.Size * 0.5f * 0.92f;
        var center = cell.Position + new Vector3(0f, yOffset, 0f);

        for (int d = 0; d < 6; d++)
        {
            var neighbor = HexUtils.GetNeighbor(cell.GridPos.X, cell.GridPos.Y, d);
            var dir = HexUtils.AxialToWorld3D(neighbor.X, neighbor.Y, cell.Elevation) - cell.Position;
            dir.Y = 0f;
            if (dir.LengthSquared() < 0.001f) continue;
            dir = dir.Normalized();

            var side = new Vector3(-dir.Z, 0f, dir.X);
            var edgeCenter = center + dir * apothem;
            var a = edgeCenter + side * halfEdge;
            var b = edgeCenter - side * halfEdge;

            mesh.SurfaceSetNormal(Vector3.Up);
            mesh.SurfaceSetColor(centerColor);
            mesh.SurfaceAddVertex(center);
            mesh.SurfaceSetColor(edgeColor);
            mesh.SurfaceAddVertex(a);
            mesh.SurfaceSetColor(edgeColor);
            mesh.SurfaceAddVertex(b);
        }
    }

    private void DrawMoveRangeBoundary(HashSet<Vector2I> reachable)
    {
        if (HexGrid == null || reachable.Count == 0) return;

        var core = new ImmediateMesh();
        var glow = new ImmediateMesh();
        core.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        glow.SurfaceBegin(Mesh.PrimitiveType.Triangles);

        bool hasGeometry = false;
        float apothem = HexUtils.Size * Mathf.Sqrt(3.0f) * 0.5f;
        float halfEdge = HexUtils.Size * 0.5f;
        float yOffset = BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + BladeHex.View.Combat.CombatLayerHeight.OverlayLayer + 0.8f;

        foreach (var cell in _baseRangeCells)
        {
            if (cell == null || !GodotObject.IsInstanceValid(cell)) continue;

            for (int d = 0; d < 6; d++)
            {
                var neighbor = HexUtils.GetNeighbor(cell.GridPos.X, cell.GridPos.Y, d);
                if (reachable.Contains(neighbor)) continue;

                var dir = HexUtils.AxialToWorld3D(neighbor.X, neighbor.Y, cell.Elevation) - cell.Position;
                dir.Y = 0f;
                if (dir.LengthSquared() < 0.001f) continue;
                dir = dir.Normalized();

                var side = new Vector3(-dir.Z, 0f, dir.X);
                var edgeCenter = cell.Position + new Vector3(0f, yOffset, 0f) + dir * apothem;
                var a = edgeCenter + side * halfEdge;
                var b = edgeCenter - side * halfEdge;

                var coreColor = AdaptMoveColor(new Color(0.78f, 0.58f, 0.27f, 0.50f), cell);
                var glowColor = AdaptMoveColor(new Color(0.44f, 0.32f, 0.14f, 0.16f), cell);
                AddSolidEdgeBand(core, a, b, dir, 5.0f, coreColor);
                AddSoftEdgeGlow(glow, a, b, dir, 5.0f, 20.0f, glowColor);
                hasGeometry = true;
            }
        }

        core.SurfaceEnd();
        glow.SurfaceEnd();
        if (!hasGeometry) return;

        _moveRangeGlowMesh = new MeshInstance3D
        {
            Name = "MoveRangeGlow",
            Mesh = glow,
            MaterialOverride = CreateMoveVertexMaterial(5),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_moveRangeGlowMesh);

        _moveRangeBorderMesh = new MeshInstance3D
        {
            Name = "MoveRangeBorder",
            Mesh = core,
            MaterialOverride = CreateMoveVertexMaterial(7),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_moveRangeBorderMesh);
    }


    private static void AddSolidEdgeBand(ImmediateMesh mesh, Vector3 a, Vector3 b, Vector3 normal, float width, Color color)
    {
        var n = normal * (width * 0.5f);
        AddBand(mesh, a - n, b - n, a + n, b + n, color, color);
    }

    private static void AddSoftEdgeGlow(ImmediateMesh mesh, Vector3 a, Vector3 b, Vector3 normal, float coreHalfWidth, float glowHalfWidth, Color color)
    {
        var transparent = new Color(color.R, color.G, color.B, 0f);
        AddBand(mesh, a - normal * glowHalfWidth, b - normal * glowHalfWidth, a - normal * coreHalfWidth, b - normal * coreHalfWidth, transparent, color);
        AddBand(mesh, a - normal * coreHalfWidth, b - normal * coreHalfWidth, a + normal * coreHalfWidth, b + normal * coreHalfWidth, color, color);
        AddBand(mesh, a + normal * coreHalfWidth, b + normal * coreHalfWidth, a + normal * glowHalfWidth, b + normal * glowHalfWidth, color, transparent);
    }

    private static void AddBand(ImmediateMesh mesh, Vector3 o1, Vector3 o2, Vector3 i1, Vector3 i2, Color outerColor, Color innerColor)
    {
        mesh.SurfaceSetNormal(Vector3.Up);
        mesh.SurfaceSetColor(outerColor);
        mesh.SurfaceAddVertex(o1);
        mesh.SurfaceSetColor(outerColor);
        mesh.SurfaceAddVertex(o2);
        mesh.SurfaceSetColor(innerColor);
        mesh.SurfaceAddVertex(i2);

        mesh.SurfaceSetColor(outerColor);
        mesh.SurfaceAddVertex(o1);
        mesh.SurfaceSetColor(innerColor);
        mesh.SurfaceAddVertex(i2);
        mesh.SurfaceSetColor(innerColor);
        mesh.SurfaceAddVertex(i1);
    }

    private static StandardMaterial3D CreateMoveVertexMaterial(int renderPriority)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            RenderPriority = renderPriority,
        };
    }

    private Color AdaptMoveColor(Color color, HexCell cell)
    {
        float terrainFactor = IsSnowLike(cell) ? 0.58f : 1.0f;
        float nightFactor = IsNightHour(CurrentHour) ? 1.45f : 1.0f;
        float alphaFactor = IsNightHour(CurrentHour) ? 1.22f : 1.0f;

        float brightness = terrainFactor * nightFactor;
        return new Color(
            Mathf.Clamp(color.R * brightness, 0f, 1f),
            Mathf.Clamp(color.G * brightness, 0f, 1f),
            Mathf.Clamp(color.B * brightness, 0f, 1f),
            Mathf.Clamp(color.A * alphaFactor, 0f, 0.75f));
    }

    private static bool IsNightHour(float hour)
    {
        return hour >= 18.0f || hour < 6.0f;
    }

    private static bool IsSnowLike(HexCell cell)
    {
        var terrain = cell.Data?.terrainType;
        return terrain == BattleCellData.TerrainType.Snow
            || terrain == BattleCellData.TerrainType.MountainSnow
            || terrain == BattleCellData.TerrainType.Ice
            || (cell.Data?.specialEffect?.Contains("snow", StringComparison.OrdinalIgnoreCase) ?? false);
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
    private Vector3 _previewStartCellPos;
    private float _pathPreviewProgress;
    private bool _pathPreviewAnimating;

    // ===== 悬浮高亮 =====
    private HexCell? _currentHoverCell;
    private Decal? _hoverDecal;

    // ===== 当前行动单位标记 =====
    private HexCell? _selectedUnitCell;
    private Unit? _selectedUnit;
    private Decal? _selectedUnitDecal;

    public void DrawPathPreview(List<Vector2I> path, Vector3 startCellPos)
    {
        if (IsSamePathPreview(path, startCellPos))
            return;

        ClearPathPreview();
        if (HexGrid == null) return;

        _previewPath = new List<Vector2I>(path);
        _previewStartCellPos = startCellPos;
        _pathPreviewProgress = 0f;
        _pathPreviewAnimating = true;

        if (_pathPreviewLine == null)
        {
            _pathPreviewLine = new MeshInstance3D();
            _pathPreviewLine.Name = "PathPreviewArrows";
            AddChild(_pathPreviewLine);
        }

        _pathPreviewLine.MaterialOverride = CreateMoveVertexMaterial(8);
        _pathPreviewLine.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        RebuildPathPreviewArrows();
    }

    private bool IsSamePathPreview(List<Vector2I> path, Vector3 startCellPos)
    {
        if (_previewPath == null || _pathPreviewLine == null || !_pathPreviewLine.Visible)
            return false;

        if (_previewStartCellPos.DistanceSquaredTo(startCellPos) > 0.001f || _previewPath.Count != path.Count)
            return false;

        for (int i = 0; i < path.Count; i++)
        {
            if (_previewPath[i] != path[i])
                return false;
        }

        return true;
    }

    private void UpdatePathPreviewArrows(double delta)
    {
        if (!_pathPreviewAnimating || _previewPath == null || _pathPreviewLine == null)
            return;

        _pathPreviewProgress = Mathf.Min(1f, _pathPreviewProgress + (float)delta * 2.2f);
        RebuildPathPreviewArrows();

        if (_pathPreviewProgress >= 1f)
            _pathPreviewAnimating = false;
    }

    private void RebuildPathPreviewArrows()
    {
        if (HexGrid == null || _pathPreviewLine == null || _previewPath == null)
            return;

        float yOffset = CombatLayerHeight.HexTopOffset + CombatLayerHeight.UIHintLayer;
        var worldPositions = new List<Vector3> { _previewStartCellPos };

        foreach (var coord in _previewPath)
        {
            var c = HexGrid.GetCell(coord.X, coord.Y);
            if (c != null)
                worldPositions.Add(c.Position);
        }

        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);

        float easedProgress = EaseOutCubic(_pathPreviewProgress);
        float totalLength = GetPathLength(worldPositions);
        if (totalLength <= 0.001f)
        {
            mesh.SurfaceEnd();
            _pathPreviewLine.Mesh = mesh;
            _pathPreviewLine.Visible = false;
            _pathPreviewAnimating = false;
            return;
        }

        float visibleLength = totalLength * easedProgress;
        bool settled = _pathPreviewProgress >= 1f;
        float alpha = settled ? 0.80f : Mathf.Lerp(0.42f, 0.84f, easedProgress);
        AddPathArrowRibbon(mesh, worldPositions, visibleLength, alpha, yOffset);

        mesh.SurfaceEnd();
        _pathPreviewLine.Mesh = mesh;
        _pathPreviewLine.Visible = true;
    }

    private static float GetPathLength(List<Vector3> worldPositions)
    {
        float totalLength = 0f;
        for (int i = 1; i < worldPositions.Count; i++)
        {
            var dir = new Vector3(
                worldPositions[i].X - worldPositions[i - 1].X,
                0f,
                worldPositions[i].Z - worldPositions[i - 1].Z);
            if (dir.LengthSquared() >= 0.001f)
                totalLength += dir.Length();
        }

        return totalLength;
    }

    private static bool TryGetPointOnPath(List<Vector3> worldPositions, float targetDistance, out Vector3 point, out Vector3 direction)
    {
        point = Vector3.Zero;
        direction = Vector3.Forward;
        float walked = 0f;

        for (int i = 1; i < worldPositions.Count; i++)
        {
            var prev = worldPositions[i - 1];
            var curr = worldPositions[i];
            var delta = new Vector3(curr.X - prev.X, 0f, curr.Z - prev.Z);
            float segmentLength = delta.Length();
            if (segmentLength < 0.001f)
                continue;

            direction = delta / segmentLength;
            if (targetDistance <= walked + segmentLength)
            {
                float t = Mathf.Clamp((targetDistance - walked) / segmentLength, 0f, 1f);
                point = prev.Lerp(curr, t);
                return true;
            }

            walked += segmentLength;
            point = curr;
        }

        return true;
    }

    private static float EaseOutCubic(float t)
    {
        float inv = 1f - Mathf.Clamp(t, 0f, 1f);
        return 1f - inv * inv * inv;
    }

    private static void AddPathArrowRibbon(ImmediateMesh mesh, List<Vector3> worldPositions, float visibleLength, float maxAlpha, float yOffset)
    {
        if (visibleLength <= 0.001f)
            return;

        float shaftHalfWidth = HexUtils.Size * 0.090f;
        float headLength = HexUtils.Size * 0.58f;
        float headHalfWidth = HexUtils.Size * 0.285f;
        float headBaseDistance = Mathf.Max(0f, visibleLength - headLength);

        AddSingleArrowShaft(mesh, worldPositions, headBaseDistance, shaftHalfWidth, maxAlpha, yOffset);

        if (!TryGetPointOnPath(worldPositions, visibleLength, out var tip, out var tipDirection))
            return;

        if (!TryGetPointOnPath(worldPositions, headBaseDistance, out var headBase, out _))
            return;

        float grow = Mathf.Clamp(visibleLength / headLength, 0.35f, 1f);
        var headRight = new Vector3(-tipDirection.Z, 0f, tipDirection.X);
        var baseColor = PathArrowColor(headBaseDistance, maxAlpha);
        var tipColor = PathArrowSolidColor(Mathf.Min(maxAlpha + 0.10f, 0.90f));
        headBase += Vector3.Up * yOffset;
        tip += Vector3.Up * yOffset;

        AddArrowTriangle(
            mesh,
            headBase - headRight * headHalfWidth * grow,
            headBase + headRight * headHalfWidth * grow,
            tip,
            baseColor,
            tipColor);
    }

    private static void AddSingleArrowShaft(ImmediateMesh mesh, List<Vector3> worldPositions, float length, float halfWidth, float maxAlpha, float yOffset)
    {
        if (length <= 0.001f)
            return;

        int steps = Mathf.Max(2, Mathf.CeilToInt(length / (HexUtils.Size * 0.28f)));
        if (!TryGetPointOnPath(worldPositions, 0f, out var prevPoint, out var prevDirection))
            return;

        prevPoint += Vector3.Up * yOffset;
        var prevRight = new Vector3(-prevDirection.Z, 0f, prevDirection.X);
        var prevColor = PathArrowColor(0f, maxAlpha);
        var prevLeft = prevPoint - prevRight * halfWidth;
        var prevRailRight = prevPoint + prevRight * halfWidth;

        for (int i = 1; i <= steps; i++)
        {
            float distance = length * i / steps;
            if (!TryGetPointOnPath(worldPositions, distance, out var point, out var direction))
                break;

            point += Vector3.Up * yOffset;
            var right = new Vector3(-direction.Z, 0f, direction.X);
            var color = PathArrowColor(distance, maxAlpha);
            var left = point - right * halfWidth;
            var railRight = point + right * halfWidth;

            AddArrowQuad(mesh, prevLeft, prevRailRight, railRight, left, prevColor, color);

            prevLeft = left;
            prevRailRight = railRight;
            prevColor = color;
        }
    }

    private static Color PathArrowColor(float distanceFromStart, float maxAlpha)
    {
        float tailFadeLength = HexUtils.Size * 1.55f;
        float tailFade = Mathf.Clamp(distanceFromStart / tailFadeLength, 0f, 1f);
        float alpha = Mathf.Lerp(maxAlpha * 0.14f, maxAlpha, tailFade);
        return PathArrowSolidColor(alpha);
    }

    private static Color PathArrowSolidColor(float alpha)
    {
        return new Color(0.24f, 0.19f, 0.13f, alpha);
    }

    private static void AddArrowQuad(ImmediateMesh mesh, Vector3 tailLeft, Vector3 tailRight, Vector3 headRight, Vector3 headLeft, Color tailColor, Color headColor)
    {
        mesh.SurfaceSetNormal(Vector3.Up);
        mesh.SurfaceSetColor(tailColor);
        mesh.SurfaceAddVertex(tailLeft);
        mesh.SurfaceSetColor(tailColor);
        mesh.SurfaceAddVertex(tailRight);
        mesh.SurfaceSetColor(headColor);
        mesh.SurfaceAddVertex(headRight);

        mesh.SurfaceSetColor(tailColor);
        mesh.SurfaceAddVertex(tailLeft);
        mesh.SurfaceSetColor(headColor);
        mesh.SurfaceAddVertex(headRight);
        mesh.SurfaceSetColor(headColor);
        mesh.SurfaceAddVertex(headLeft);
    }

    private static void AddArrowTriangle(ImmediateMesh mesh, Vector3 left, Vector3 right, Vector3 tip, Color baseColor, Color tipColor)
    {
        mesh.SurfaceSetNormal(Vector3.Up);
        mesh.SurfaceSetColor(baseColor);
        mesh.SurfaceAddVertex(left);
        mesh.SurfaceSetColor(baseColor);
        mesh.SurfaceAddVertex(right);
        mesh.SurfaceSetColor(tipColor);
        mesh.SurfaceAddVertex(tip);
    }

    public void ClearPathPreview()
    {
        _previewPath = null;
        _pathPreviewAnimating = false;
        _pathPreviewProgress = 0f;
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
            _hoverDecal.Size = new Vector3(hexDiameter, 3.0f, hexDiameter);

            _hoverDecal.Modulate = new Color(1.0f, 0.8f, 0.35f, 0.45f);
            _hoverDecal.EmissionEnergy = 0.8f;
            _hoverDecal.AlbedoMix = 0.3f;
            _hoverDecal.UpperFade = 0.0f;
            _hoverDecal.LowerFade = 0.0f;
            _hoverDecal.TextureNormal = null;
            _hoverDecal.RotationDegrees = new Vector3(0, 30, 0);

            AddChild(_hoverDecal);
        }

        // Decal 位置：略微在 hex 顶面上方 1.0f 处，垂直深度仅 3.0f，因此其投射边界为 [HexTopOffset - 0.5f, HexTopOffset + 2.5f]
        float decalY = cell.Position.Y + BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + 1.0f;
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
            _selectedUnitDecal.Size = new Vector3(hexDiameter, 3.0f, hexDiameter);
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
        // 地表面相对角色的本地 Y 轴高度 = -CharacterLayer (-5f)。
        // 投影中心设在地面上方 1f，即 1f - CharacterLayer (-4f)。
        float localGroundY = 1.0f - BladeHex.View.Combat.CombatLayerHeight.CharacterLayer;
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
        UpdatePathPreviewArrows(delta);
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
