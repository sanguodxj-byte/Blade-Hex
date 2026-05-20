// HexCellMultiMeshBatcher.cs
// 战斗网格 MultiMesh 合批管理器
// 按 (TerrainType, Elevation) 将 HexCell 合并到 MultiMeshInstance3D，
// 高亮/选中通过 per-instance custom data 传递 flag，由 shader 着色。
// 
// 验收标准：draw call 从 N×格子数 降到 terrain 类型 × elevation 桶数（通常 < 30）。
using Godot;
using System.Collections.Generic;
using BladeHex.Data;
using BladeHex.Map;

namespace BladeHex.View.Map;

/// <summary>
/// [Scene Service] 战斗网格 MultiMesh 合批管理器。
///
/// <para>所属场景：<see cref="BladeHex.Map.HexGrid"/>（每个 HexGrid 持有一个）。</para>
/// <para>生命周期：随 HexGrid 创建与销毁。</para>
/// <para>访问方式：<see cref="HexCell.Batcher"/> 注入引用。</para>
/// <para>职责：按 (TerrainType, Elevation) 将 HexCell 合并到 MultiMeshInstance3D，draw call 从 N×格子数 降到 桶数 (&lt; 30)。</para>
/// </summary>
[GlobalClass]
public partial class HexCellMultiMeshBatcher : Node3D
{
    // ============================================================================
    // 内部数据结构
    // ============================================================================

    /// <summary>每个 (TerrainType, Elevation) 桶的缓存数据</summary>
    private class BucketData
    {
        public MultiMeshInstance3D Instance3D { get; set; } = null!;
        public List<HexCell> Cells { get; set; } = new();
        public bool Dirty { get; set; }
    }

    /// <summary>桶字典：key = $"{terrainInt}_{elevation}"</summary>
    private readonly Dictionary<string, BucketData> _buckets = new();

    /// <summary>HexCell → 它所属的桶 key（用于快速查找反注册）</summary>
    private readonly Dictionary<HexCell, string> _cellBucketMap = new();

    /// <summary>六棱柱网格几何体缓存（所有桶共享同一份网格）</summary>
    private CylinderMesh? _sharedMesh;

    /// <summary>攻城建筑 mesh 缓存</summary>
    private ArrayMesh? _rampartMesh;
    private ArrayMesh? _towerMesh;
    private ArrayMesh? _gateMesh;

    /// <summary>网格参数常量</summary>
    private const int RadialSegments = 6;
    private const float HexRadius = 96.0f;   // 与 HexUtils.Size 保持一致
    private const float HexHeight = 48.0f;   // HexUtils.Size * 0.5f

    // ============================================================================
    // 生命周期
    // ============================================================================

    public override void _Ready()
    {
        Name = "HexCellMultiMeshBatcher";
        _sharedMesh = CreateHexMesh();
        _rampartMesh = BladeHex.View.Map.SiegeMeshFactory.CreateRampartMesh();
        _towerMesh = BladeHex.View.Map.SiegeMeshFactory.CreateTowerMesh();
        _gateMesh = BladeHex.View.Map.SiegeMeshFactory.CreateGateMesh();
    }

    public override void _Process(double delta)
    {
        // 每帧检查脏标记并重建
        bool anyDirty = false;
        foreach (var kvp in _buckets)
        {
            if (kvp.Value.Dirty)
            {
                RebuildBucket(kvp.Key, kvp.Value);
                anyDirty = true;
            }
        }
        if (anyDirty)
        {
            // 清理空桶
            var toRemove = new List<string>();
            foreach (var kvp in _buckets)
            {
                if (kvp.Value.Cells.Count == 0)
                    toRemove.Add(kvp.Key);
            }
            foreach (var key in toRemove)
            {
                _buckets[key].Instance3D.QueueFree();
                _buckets.Remove(key);
            }
        }
    }

    // ============================================================================
    // 公共接口
    // ============================================================================

    /// <summary>注册一个 HexCell 到合批系统</summary>
    public void RegisterCell(HexCell cell, BattleCellData.TerrainType terrainType, int elevation, Vector3 worldPosition)
    {
        string key = $"{(int)terrainType}_{elevation}";

        if (!_buckets.TryGetValue(key, out var bucket))
        {
            bucket = new BucketData();
            bucket.Instance3D = new MultiMeshInstance3D();
            bucket.Instance3D.Name = $"Bucket_{key}";
            AddChild(bucket.Instance3D);
            _buckets[key] = bucket;
        }

        // 记录映射
        _cellBucketMap[cell] = key;

        // 添加到桶
        bucket.Cells.Add(cell);
        bucket.Dirty = true;
    }

    /// <summary>反注册一个 HexCell（移除时调用）</summary>
    public void UnregisterCell(HexCell cell)
    {
        if (!_cellBucketMap.TryGetValue(cell, out string? key))
            return;

        if (_buckets.TryGetValue(key, out var bucket))
        {
            bucket.Cells.Remove(cell);
            bucket.Dirty = true;
        }

        _cellBucketMap.Remove(cell);
    }

    /// <summary>设置格子高亮状态</summary>
    public void SetCellHighlight(HexCell cell, bool active, Color? color = null)
    {
        if (!_cellBucketMap.TryGetValue(cell, out string? key))
            return;

        if (!_buckets.TryGetValue(key, out var bucket))
            return;

        var mm = bucket.Instance3D.Multimesh;
        if (mm == null) return;

        int idx = bucket.Cells.IndexOf(cell);
        if (idx < 0 || idx >= mm.InstanceCount) return;

        Color hlColor = color ?? new Color(1, 1, 1, 0.3f);

        // CustomData 打包: R=highlight flag, G=hlR, B=hlG, A=hlB
        var packed = new Color(
            active ? 1f : 0f,
            active ? hlColor.R : 0f,
            active ? hlColor.G : 0f,
            active ? hlColor.B : 0f
        );
        mm.SetInstanceCustomData(idx, packed);

        // 独立高亮覆盖层（在纹理层之上）
        UpdateHighlightOverlay(cell, active, hlColor);
    }

    /// <summary>设置格子迷雾/遮蔽状态（当前通过 modulate 实现，custom data 预留）</summary>
    public void SetCellShrouded(HexCell cell, bool isShrouded)
    {
        // 迷雾遮蔽暂不通过 custom data 实现（shader 中已注释掉 shroud 混合）
        // 后续可通过 MultiMeshInstance3D.Modulate 或独立 overlay 实现
        // 此处保留接口，避免调用方报错
    }

    /// <summary>强制重建所有桶（在地形/高程批量变化后调用）</summary>
    public void RebuildAll()
    {
        foreach (var kvp in _buckets)
        {
            kvp.Value.Dirty = true;
        }
    }

    // ============================================================================
    // 高亮覆盖层（独立半透明圆盘，在纹理层之上）
    // ============================================================================

    private readonly Dictionary<HexCell, MeshInstance3D> _highlightOverlays = new();
    private static ArrayMesh? _highlightRingMesh;

    /// <summary>创建六边形环 mesh（中间镂空）</summary>
    private static ArrayMesh CreateHexRingMesh()
    {
        // 六边形环：外半径 0.92，内半径 0.70（中间镂空）
        float outerR = HexRadius * 0.92f;
        float innerR = HexRadius * 0.70f;
        float height = 0.3f;
        int sides = 6;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < sides; i++)
        {
            float a1 = Mathf.DegToRad(60f * i + 30f); // +30 for flat-top
            float a2 = Mathf.DegToRad(60f * (i + 1) + 30f);

            var outerV1 = new Vector3(Mathf.Cos(a1) * outerR, height * 0.5f, Mathf.Sin(a1) * outerR);
            var outerV2 = new Vector3(Mathf.Cos(a2) * outerR, height * 0.5f, Mathf.Sin(a2) * outerR);
            var innerV1 = new Vector3(Mathf.Cos(a1) * innerR, height * 0.5f, Mathf.Sin(a1) * innerR);
            var innerV2 = new Vector3(Mathf.Cos(a2) * innerR, height * 0.5f, Mathf.Sin(a2) * innerR);

            st.SetNormal(Vector3.Up);

            // 三角形 1: outer1 - inner1 - outer2
            st.AddVertex(outerV1);
            st.AddVertex(innerV1);
            st.AddVertex(outerV2);

            // 三角形 2: inner1 - inner2 - outer2
            st.AddVertex(innerV1);
            st.AddVertex(innerV2);
            st.AddVertex(outerV2);
        }

        return st.Commit();
    }

    private void UpdateHighlightOverlay(HexCell cell, bool active, Color color)
    {
        if (active)
        {
            if (!_highlightOverlays.TryGetValue(cell, out var overlay))
            {
                _highlightRingMesh ??= CreateHexRingMesh();

                overlay = new MeshInstance3D();
                overlay.Mesh = _highlightRingMesh;
                overlay.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                AddChild(overlay);
                _highlightOverlays[cell] = overlay;
            }

            // 位置：纹理层之上（OverlayLayer）
            float overlayY = BladeHex.View.Combat.CombatLayerHeight.HexTopOffset + BladeHex.View.Combat.CombatLayerHeight.OverlayLayer;
            overlay.Position = cell.Position + new Vector3(0, overlayY, 0);

            // 半透明材质
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(color.R, color.G, color.B, Mathf.Clamp(color.A * 1.2f, 0.2f, 0.7f));
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            mat.NoDepthTest = true;
            overlay.MaterialOverride = mat;
            overlay.Visible = true;
        }
        else
        {
            if (_highlightOverlays.TryGetValue(cell, out var overlay))
            {
                overlay.Visible = false;
            }
        }
    }

    // ============================================================================
    // 内部方法
    // ============================================================================

    /// <summary>创建共享六棱柱网格</summary>
    private static CylinderMesh CreateHexMesh()
    {
        var mesh = new CylinderMesh();
        mesh.RadialSegments = RadialSegments;
        mesh.Rings = 1;
        mesh.TopRadius = HexRadius;
        mesh.BottomRadius = HexRadius;
        mesh.Height = HexHeight;
        return mesh;
    }

    /// <summary>根据桶 key 选择正确的 mesh</summary>
    private Mesh ResolveMeshForBucket(string key)
    {
        // 暂时所有地形都用标准六棱柱，通过 Y 缩放表现高度
        // 城墙自定义 mesh 后续再启用（需要适配动态高度）
        return _sharedMesh!;
    }

    /// <summary>判断桶是否为攻城建筑类型</summary>
    private static bool IsSiegeTerrainBucket(string key)
    {
        // 暂时关闭自定义 mesh，所有格子统一用 Y 缩放渲染
        return false;
    }

    /// <summary>重建指定桶的 MultiMesh</summary>
    private void RebuildBucket(string key, BucketData bucket)
    {
        int count = bucket.Cells.Count;
        if (count == 0)
        {
            bucket.Instance3D.Multimesh = null;
            bucket.Dirty = false;
            return;
        }

        // 创建或复用 MultiMesh
        var mm = bucket.Instance3D.Multimesh;
        if (mm == null || mm.InstanceCount != count)
        {
            mm = new MultiMesh();
            // 根据地形类型选择 mesh
            mm.Mesh = ResolveMeshForBucket(key);
            mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
            mm.UseCustomData = true;
            mm.InstanceCount = count;
            bucket.Instance3D.Multimesh = mm;
        }

        // 获取材质
        var parts = key.Split('_');
        if (parts.Length == 2 && int.TryParse(parts[0], out int terrainInt) && int.TryParse(parts[1], out int elevation))
        {
            var terrainType = (BattleCellData.TerrainType)terrainInt;
            var mat = CombatMaterialManager.Instance.GetMaterial(terrainType, elevation);
            bucket.Instance3D.MaterialOverride = mat;
        }

        // 填充每个实例的 transform 和 custom data
        for (int i = 0; i < count; i++)
        {
            var cell = bucket.Cells[i];
            if (cell == null || !GodotObject.IsInstanceValid(cell)) continue;

            // 柱体高度修正：elevation=0 高度 1×, elevation=1 高度 2×, elevation=2 高度 3×
            // 确保柱体底面始终在 Y=0（地面），不会悬浮
            // 共享 mesh 高度 = HexHeight(48)，通过 Y 缩放拉伸
            int cellElev = cell.Elevation;
            float heightScale = cellElev + 1.0f;  // elev 0→1×, 1→2×, 2→3×

            // 攻城建筑 mesh 已经内置了正确高度，不需要 Y 缩放
            bool isSiegeMesh = IsSiegeTerrainBucket(key);
            if (isSiegeMesh) heightScale = 1.0f;

            // 拉伸后柱体总高 = HexHeight × heightScale
            // 柱体中心需要下移，使顶面保持在 cell.Position.Y + HexHeight/2
            // 原始：中心在 cell.Position.Y，顶面在 Y+24，底面在 Y-24
            // 拉伸后：中心在 cell.Position.Y - (heightScale-1)*HexHeight/2
            //         顶面在 中心 + heightScale*HexHeight/2 = cell.Position.Y + HexHeight/2 ✓
            //         底面在 中心 - heightScale*HexHeight/2 = cell.Position.Y - (2*heightScale-1)*HexHeight/2
            float yOffset = isSiegeMesh
                ? -cell.Position.Y  // 攻城 mesh 底面在 Y=0，直接放到世界 Y=0
                : -(heightScale - 1.0f) * HexHeight * 0.5f;

            var pos = cell.Position + new Vector3(0, yOffset, 0);
            var basis = Basis.Identity
                .Rotated(Vector3.Up, Mathf.DegToRad(30f))
                .Scaled(new Vector3(1f, heightScale, 1f));

            // 攻城 mesh 不旋转（已经内置了 30° 偏移）
            if (isSiegeMesh)
                basis = Basis.Identity;

            var xform = new Transform3D(basis, pos);
            mm.SetInstanceTransform(i, xform);

            // CustomData: 打包高亮+遮蔽到单个 Color
            var custom = new Color(0f, 0f, 0f, 0f); // 默认：无高亮，无遮蔽
            mm.SetInstanceCustomData(i, custom);
        }

        bucket.Dirty = false;
    }
}
