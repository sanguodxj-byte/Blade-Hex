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
            mm.Mesh = _sharedMesh;
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

            // Transform: 位置 + 30° Y 旋转（平顶六边形适配）
            var pos = cell.Position;
            var xform = new Transform3D(
                Basis.Identity.Rotated(Vector3.Up, Mathf.DegToRad(30f)),
                pos
            );
            mm.SetInstanceTransform(i, xform);

            // CustomData: 打包高亮+遮蔽到单个 Color
            // R = highlight flag (0/1)
            // G = highlight color R
            // B = highlight color G
            // A = pack: bit7=shroud flag, bit0-6=highlight color B
            //     encoding: A = shroud * 0.5 + highlightB * 0.49
            var custom = new Color(0f, 0f, 0f, 0f); // 默认：无高亮，无遮蔽
            mm.SetInstanceCustomData(i, custom);
        }

        bucket.Dirty = false;
    }
}
